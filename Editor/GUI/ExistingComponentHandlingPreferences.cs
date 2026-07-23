using System;
using net.bekobeko.utilitytools.core;
using UnityEditor;

namespace net.bekobeko.utilitytools.windows
{
    public static class ExistingComponentHandlingPreferences
    {
        public static ExistingComponentHandlingMode Load(string preferenceKey)
        {
            if (string.IsNullOrEmpty(preferenceKey)) return ExistingComponentHandlingMode.Skip;

            var stored = EditorPrefs.GetInt(preferenceKey, (int)ExistingComponentHandlingMode.Skip);
            return Enum.IsDefined(typeof(ExistingComponentHandlingMode), stored)
                ? (ExistingComponentHandlingMode)stored
                : ExistingComponentHandlingMode.Skip;
        }

        public static void Save(string preferenceKey, ExistingComponentHandlingMode mode)
        {
            if (string.IsNullOrEmpty(preferenceKey)) return;
            var value = Enum.IsDefined(typeof(ExistingComponentHandlingMode), mode)
                ? mode
                : ExistingComponentHandlingMode.Skip;
            EditorPrefs.SetInt(preferenceKey, (int)value);
        }
    }
}
