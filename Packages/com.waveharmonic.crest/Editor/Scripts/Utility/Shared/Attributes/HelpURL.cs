// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using System.Reflection;
using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Constructs a custom link to Crest's documentation for the help URL button.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, AllowMultiple = false)]
    sealed class HelpURL : HelpURLAttribute
    {
        const string k_Prefix = "https://docs.crest.waveharmonic.com";

        public readonly Type _Type;

        public HelpURL(string path = "") : base(GetPageLink(path))
        {
            // Blank.
        }

        public HelpURL(Type type) : base(GetPageLink(type))
        {
            _Type = type;
        }

        static string GetBaseURL()
        {
#if CREST_DEBUG
            if (Editor.Development.Utility.IsLocalServerRunning())
            {
                return Editor.Development.Utility.k_LocalServerPrefix;
            }
            else
#endif
            {
                return k_Prefix;
            }
        }

        public static string GetPageLink(string path)
        {
            return GetBaseURL() + "/" + path;
        }

        public static string GetPageLink(Type type)
        {
            var isComponent = typeof(Component).IsAssignableFrom(type);

            var menu = isComponent
                ? type.GetCustomAttribute<AddComponentMenu>().componentMenu
                : type.GetCustomAttribute<CreateAssetMenuAttribute>().menuName;

            return $"{GetBaseURL()}/{(isComponent ? "Components" : "Assets")}/{menu[6..].Replace("Crest ", "").Replace(" ", "").Replace("(", "").Replace(")", "")}.html";
        }
    }
}
