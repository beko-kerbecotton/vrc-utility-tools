using System;
using net.bekobeko.utilitytools.localization;
using UnityEditor;

namespace net.bekobeko.utilitytools.windows
{
    public enum ToolUIMode
    {
        Normal,
        Advanced
    }

    public static class ToolUIModeGUI
    {
        public static ToolUIMode Load(string preferenceKey)
        {
            if (string.IsNullOrEmpty(preferenceKey)) return ToolUIMode.Normal;
            var stored = EditorPrefs.GetInt(preferenceKey, (int)ToolUIMode.Normal);
            return Enum.IsDefined(typeof(ToolUIMode), stored)
                ? (ToolUIMode)stored
                : ToolUIMode.Normal;
        }

        public static bool Draw(string preferenceKey, ref ToolUIMode mode)
        {
            var selected = (ToolUIMode)EditorGUILayout.Popup(
                EditorLocalization.Get("uiMode.label"),
                (int)mode,
                new[]
                {
                    EditorLocalization.Get("uiMode.normal"),
                    EditorLocalization.Get("uiMode.advanced")
                });
            if (selected == mode) return false;

            mode = selected;
            if (!string.IsNullOrEmpty(preferenceKey))
                EditorPrefs.SetInt(preferenceKey, (int)mode);
            return true;
        }
    }
}
