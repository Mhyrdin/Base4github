// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR

using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;

namespace WaveHarmonic.Crest.Internal
{
    using Include = ExecuteDuringEditMode.Include;

    /// <summary>
    /// Implements custom behaviors common to all components.
    /// </summary>
    public abstract partial class EditorBehaviour : MonoBehaviour
    {
        bool _IsFirstOnValidate = true;
        bool _RunInEditModeQueued;
        internal bool _IsPrefabStageInstance;

        private protected virtual bool CanRunInEditMode => true;

        private protected virtual void Awake()
        {
            Log("Awake");

            TestAwakeOpen();

            // Prevents allocations.
            useGUILayout = false;

            DestroyManagedGameObjects();

            TestAwakeShut();

            _EditorAfterAwake = true;
        }

        /// <summary>
        /// Start method. Must be called if overridden.
        /// </summary>
        private protected virtual void Start()
        {
            Log("Start");

            TestStartOpen();

            Validate();

            _RunInEditModeQueued = false;
            _StartQueued = false;
            _StartInEnable = false;

            // Only happens in tests.
            EditorApplication.delayCall -= Start;

            TestStartShut();
        }

        // Unity does not call OnDisable/OnEnable on Reset.
        private protected virtual void Reset()
        {
            if (_IsFirstOnValidate || !enabled) return;
            enabled = false;
            OnDestroy();
            Awake();
            enabled = true;
            Start();
        }

        /// <summary>
        /// OnValidate method. Must be called if overridden.
        /// </summary>
        private protected virtual void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (!CanRunInEditMode)
            {
                return;
            }

            // We do this in OnValidate because it always runs unlike Reset.
            if (_IsFirstOnValidate)
            {
                var attribute = Helpers.GetCustomAttribute<ExecuteDuringEditMode>(GetType());

                var enableInEditMode = attribute != null;

                if (enableInEditMode && !attribute._Including.HasFlag(Include.BuildPipeline))
                {
                    // Do not execute when building the player.
                    enableInEditMode = !BuildPipeline.isBuildingPlayer;
                }

                // Components that use the singleton pattern are candidates for not executing in the prefab stage
                // as a new instance will be created which could interfere with the scene stage instance.
                if (enableInEditMode && !attribute._Including.HasFlag(Include.PrefabStage))
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    _IsPrefabStageInstance = stage != null && gameObject.scene == stage.scene;

                    // Do not execute in prefab stage.
                    enableInEditMode = !_IsPrefabStageInstance;
                }

                // runInEditMode will immediately call Awake and OnEnable so we must not do this in OnValidate as there
                // are many restrictions which Unity will produce warnings for:
                // https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnValidate.html
                if (enableInEditMode)
                {
                    if (BuildPipeline.isBuildingPlayer)
                    {
                        // EditorApplication.update/delayCall and Invoke are not called when building.
                        EnableEditMode();
                    }
                    else
                    {
                        Log("Queued.EnableEditMode");

                        // Called between OnAwake/OnEnable and Start which makes it seamless.
                        EditorApplication.delayCall += EnableEditMode;
                        _RunInEditModeQueued = true;
                    }
                }
            }

            _IsFirstOnValidate = false;
        }

        void EnableEditMode()
        {
            // If the scene that is being built is already opened then, there can be a rogue instance which registers
            // an event but is destroyed by the time it gets here. It has something to do with OnValidate being called
            // after the object is destroyed with _isFirstOnValidate being true.
            if (this == null) return;
            // Workaround to ExecuteAlways also executing during building which is often not what we want.
            runInEditMode = true;
        }
    }

    // Managed GameObjects.
    partial class EditorBehaviour
    {
        static readonly List<ManagedGameObject> s_ManagedGameObjects = new();

        void DestroyManagedGameObjects()
        {
            transform.GetComponentsInChildren(includeInactive: true, s_ManagedGameObjects);

            // When copy and pasting from one scene to another, destroy instance objects as
            // they will have bad state.
            foreach (var generated in s_ManagedGameObjects)
            {
                if (generated.Owner == this)
                {
                    Helpers.DestroyGameObject(generated);
                }
            }

            s_ManagedGameObjects.Clear();
        }
    }

    // Validation.
    partial class EditorBehaviour
    {
        static readonly System.Type s_ValidatedHelper = System.Type.GetType("WaveHarmonic.Crest.Editor.ValidatedHelper, WaveHarmonic.Crest.Shared.Editor");
        static readonly MethodInfo s_ExecuteValidators = s_ValidatedHelper.GetMethod
        (
            "ExecuteValidators",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Object) },
            null
        );

        void Validate()
        {
            if (Application.isPlaying && !(bool)s_ExecuteValidators.Invoke(null, new object[] { this }))
            {
                enabled = false;
            }
        }
    }

    // Normalize Lifecycle
    partial class EditorBehaviour
    {
        private protected virtual bool SkipLifeCycle => false;
        bool SkipEditModeLifeCycle => (!Application.isPlaying && !runInEditMode) || SkipLifeCycle;

        bool _EditorAfterAwake;

        bool _StartInEnable;
        bool _DestroyInDisable;

        // Track any prefab is reverting to handle revert prefab case.
        static bool s_PrefabReverting;

        static bool s_CreateBehaviorsQueued;

        [InitializeOnLoadMethod]
        static void Load()
        {
            RenderPipelineHelper.s_WillActiveRenderPipelineTypeChange -= DestroyBehaviors;
            RenderPipelineHelper.s_WillActiveRenderPipelineTypeChange += DestroyBehaviors;
            RenderPipelineHelper.s_ActiveRenderPipelineTypeChanged -= OnActiveRenderPipelineTypeChanged;
            RenderPipelineHelper.s_ActiveRenderPipelineTypeChanged += OnActiveRenderPipelineTypeChanged;
            PrefabUtility.prefabInstanceReverted -= PrefabReverted;
            PrefabUtility.prefabInstanceReverted += PrefabReverted;
            PrefabUtility.prefabInstanceReverting -= PrefabReverting;
            PrefabUtility.prefabInstanceReverting += PrefabReverting;
        }

        static void PrefabReverting(GameObject o)
        {
            s_PrefabReverting = true;
        }

        static void PrefabReverted(GameObject o)
        {
            s_PrefabReverting = false;
        }

        void OnCompilationFinished(object _)
        {
            Log("OnCompilationFinished");

            if (BuildPipeline.isBuildingPlayer) return;
            if (SkipEditModeLifeCycle) return;
            // On script reload, only OnDisable/OnEnable is called.
            _DestroyInDisable = true;
            _StartInEnable = true;
            _StartQueued = false;
            EditorApplication.delayCall -= Start;
        }

        static void OnActiveRenderPipelineTypeChanged()
        {
            if (BuildPipeline.isBuildingPlayer) return;

            // Change can be triggered by Camera.Renderer which means cycling the DepthProbe
            // when it is trying to render. Delay the call.
            if (!s_CreateBehaviorsQueued)
            {
                EditorApplication.delayCall += CreateBehaviors;
            }

            s_CreateBehaviorsQueued = true;
        }

        static void DestroyBehaviors()
        {
            if (BuildPipeline.isBuildingPlayer) return;

            // TODO: should we use prefab stage?
            // Run lifecycle events ourselves, grouping them as Unity does.
            var behaviors = Helpers.FindObjectsByType<EditorBehaviour>(FindObjectsInactive.Include);

            foreach (var behavior in behaviors)
            {
                if (behavior.SkipEditModeLifeCycle) continue;

                // ie OnEnable/OnDisable will be called on RP change!
                if (behavior.runInEditMode && IsChildOfManagedPrefab(behavior))
                {
                    behavior._DestroyInDisable = true;
                    behavior._StartInEnable = true;
                    continue;
                }

                if (behavior.isActiveAndEnabled) behavior.OnDisable();
                behavior.OnDestroy();
            }
        }

        static void CreateBehaviors()
        {
            if (BuildPipeline.isBuildingPlayer) return;
            var behaviors = Helpers.FindObjectsByType<EditorBehaviour>(FindObjectsInactive.Include)
                .Where(IsNotChildOfManagedPrefab);

            foreach (var behavior in behaviors)
            {
                if (behavior.SkipEditModeLifeCycle) continue;
                if (behavior._RunInEditModeQueued) continue;
                behavior.Awake();
                if (behavior.isActiveAndEnabled) behavior.OnEnable();
            }

            foreach (var behavior in behaviors)
            {
                if (behavior.SkipEditModeLifeCycle) continue;
                if (behavior._RunInEditModeQueued) continue;
                if (behavior.isActiveAndEnabled) behavior.Start();
            }

            s_CreateBehaviorsQueued = false;
        }


        private protected virtual void OnEnable()
        {
            if (!_EditorAfterAwake)
            {
                Awake();
            }

            Log("OnEnable");

            TestEnableOpen();

            // DidReloadScripts/InitializeOnLoadMethod called too late.
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            if (runInEditMode && EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableSceneReload))
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
                EditorApplication.playModeStateChanged += OnPlayModeStateChange;
            }

            TestEnableShut();

            // Restore original lifecycle behavior.
            if (!SkipLifeCycle && !_StartQueued && !_RunInEditModeQueued && (_StartInEnable || s_PrefabReverting))
            {
                Log("StartQueued");

                _StartInEnable = false;
                _StartQueued = true;

                EditorApplication.delayCall += Start;
            }
        }

        [System.NonSerialized]
        bool _StartQueued;


        private protected virtual void OnDisable()
        {
            Log("OnDisable");

            TestDisableOpen();

            CompilationPipeline.compilationFinished -= OnCompilationFinished;

            if (runInEditMode && EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableSceneReload))
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
            }

            TestDisableShut();

            if (!SkipLifeCycle && (_DestroyInDisable || s_PrefabReverting))
            {
                OnDestroy();
            }

            _DestroyInDisable = false;
        }

        private protected virtual void OnDestroy()
        {
            Log("OnDestroy");

            TestDestroyOpen();

            if (EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableSceneReload))
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
            }

            TestDestroyShut();

            _EditorAfterAwake = false;
        }

        void OnPlayModeStateChange(PlayModeStateChange state)
        {
            Log($"OnPlayModeStateChange {state}");

            if (!EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableSceneReload))
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
                return;
            }

            // Can happen if object is destroyed by another.
            if (this == null)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
                return;
            }

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Unity does not call this with scene reload disabled.
                _DestroyInDisable = true;
            }
        }
    }

    partial class EditorBehaviour
    {
        static readonly System.Type s_RevertPrefabOnRenderPipelineChange = System.Type.GetType("WaveHarmonic.Crest.Editor.RevertPrefabOnRenderPipelineChange, WaveHarmonic.Crest.Samples");

        static GameObject FindPrefabRoot(EditorBehaviour behavior)
        {
            var transform = behavior.transform;

            while (transform.parent != null)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(transform))
                {
                    return PrefabUtility.GetOutermostPrefabInstanceRoot(transform);
                }

                transform = transform.parent;
            }

            if (PrefabUtility.IsPartOfAnyPrefab(transform))
            {
                return PrefabUtility.GetOutermostPrefabInstanceRoot(transform);
            }

            return null;
        }

        static bool IsChildOfManagedPrefab(EditorBehaviour behavior)
        {
            return IsChildOfAnyPrefabWhichHasModelPrefab(behavior) || IsPartOfRevertPrefab(behavior);
        }

        static bool IsNotChildOfManagedPrefab(EditorBehaviour behavior)
        {
            return !IsChildOfManagedPrefab(behavior);
        }

        static bool IsChildOfAnyPrefabWhichHasModelPrefab(EditorBehaviour behavior)
        {
            var root = FindPrefabRoot(behavior);

            if (root == null)
            {
                return false;
            }

            var hasModelPrefab = false;

            foreach (var item in root.GetComponentsInChildren(typeof(Transform)))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(item);
                if (source != null && PrefabUtility.IsPartOfModelPrefab(source))
                {
                    hasModelPrefab = true;
                }
            }

            return hasModelPrefab;
        }

        static bool IsPartOfRevertPrefab(EditorBehaviour behaviour)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(behaviour);
            if (root == null) return false;
            var reverts = root.GetComponentsInChildren(s_RevertPrefabOnRenderPipelineChange);
            foreach (var item in reverts)
            {
                if (PrefabUtility.GetOutermostPrefabInstanceRoot(item) == root)
                {
                    return true;
                }
            }
            return false;
        }
    }

    partial class EditorBehaviour
    {
        internal virtual bool DisableEventAssertions => hideFlags.HasFlag(HideFlags.DontSave);

        bool _TestAfterAwake;
        bool _TestAfterEnable;
        bool _TestAfterStart;
        bool _TestAfterDisable = true;
        bool _TestAfterDestroy = true;

        bool ShouldTestAfterStart => !PrefabUtility.IsPartOfModelPrefab(this);

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestAwakeOpen()
        {
            if (DisableEventAssertions) return;
            Debug.AssertFormat(_TestAfterDisable, "Crest: Disable not called! {0}", this);
            Debug.AssertFormat(_TestAfterDestroy, "Crest: Destroy not called! {0}", this);
            Debug.AssertFormat(!_TestAfterAwake, "Crest: Awake called twice! {0}", this);
            // Cannot assert _TestAfterEnable.
            Debug.AssertFormat(!_TestAfterStart, "Crest: Start not called! {0}", this);
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestAwakeShut()
        {
            _TestAfterAwake = true;
            _TestAfterDestroy = false;
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestEnableOpen()
        {
            if (DisableEventAssertions) return;
            Debug.AssertFormat(_TestAfterDisable, "Crest: Disable not called! {0}", this);
            // Cannot assert _TestAfterDestroy.
            Debug.AssertFormat(_TestAfterAwake, "Crest: Awake not called! {0}", this);
            Debug.AssertFormat(!_TestAfterEnable, "Crest: Enable called twice! {0}", this);
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestEnableShut()
        {
            _TestAfterEnable = true;
            _TestAfterDisable = false;
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestStartOpen()
        {
            if (DisableEventAssertions) return;
            // Cannot asset _TestAfterDisable.
            // Cannot asset _AfterDestroy.
            Debug.AssertFormat(_TestAfterAwake, "Crest: Awake not called! {0}", this);
            Debug.AssertFormat(_TestAfterEnable, "Crest: Enable not called! {0}", this);
            Debug.AssertFormat(!_TestAfterStart, "Crest: Start called twice! {0}", this);
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestStartShut()
        {
            _TestAfterStart = true;
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestDisableOpen()
        {
            if (DisableEventAssertions) return;
            Debug.AssertFormat(_TestAfterAwake, "Crest: Awake not called! {0}", this);
            Debug.AssertFormat(_TestAfterEnable, "Crest: Enable not called! {0}", this);
            if (!ShouldTestAfterStart) Debug.AssertFormat(_TestAfterStart, "Crest: Start not called! {0}", this);
            Debug.AssertFormat(!_TestAfterDisable, "Crest: Disable called twice! {0}", this);
            // Cannot asset _TestAfterDestroy.
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestDisableShut()
        {
            _TestAfterEnable = false;
            _TestAfterDisable = true;
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestDestroyOpen()
        {
            if (DisableEventAssertions) return;
            Debug.AssertFormat(_TestAfterAwake, "Crest: Awake not called! {0}", this);
            // if (!s_SkipAfterStart) Debug.AssertFormat(_TestAfterStart, "Crest: Start not called! {0}", this);
            // Cannot asset _TestAfterEnable.
            // OnDisable is not called if enabled is not.
            Debug.AssertFormat(!_TestAfterEnable, "Crest: Disabled not called! {0}", this);
            Debug.AssertFormat(!_TestAfterDestroy, "Crest: Destroy called twice! {0}", this);
        }

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        void TestDestroyShut()
        {
            _TestAfterAwake = false;
            _TestAfterStart = false;
            _TestAfterDestroy = true;
        }
    }

    partial class EditorBehaviour
    {
        private protected virtual bool LogEvents => false;

        [System.Diagnostics.Conditional("CREST_DEBUG")]
        private protected void Log(string label)
        {
            if (!LogEvents) return;
            Debug.Log($"{GetType().Name}.{label}");
        }
    }
}

#endif

namespace WaveHarmonic.Crest
{
    // Stores a reference to the owner so the GO can be deleted safely when duplicated/pasted.
    sealed class ManagedGameObject : MonoBehaviour
    {
        [field: SerializeField]
        public Component Owner { get; set; }
    }

    static class Extentions
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Manage(this Component owner, GameObject @object)
        {
            @object.AddComponent<ManagedGameObject>().Owner = owner;
        }
    }
}
