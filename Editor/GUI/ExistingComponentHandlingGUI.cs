using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;

namespace net.bekobeko.utilitytools.windows
{
    public static class ExistingComponentHandlingGUI
    {
        public static void Draw(string preferenceKey, ref ExistingComponentHandlingMode mode)
        {
            EditorGUI.BeginChangeCheck();
            var selected = (ExistingComponentHandlingMode)EditorGUILayout.Popup(
                EditorLocalization.Get("existing.mode"),
                (int)mode,
                new[]
                {
                    EditorLocalization.Get("existing.skip"),
                    EditorLocalization.Get("existing.merge"),
                    EditorLocalization.Get("existing.overwrite")
                });
            if (!EditorGUI.EndChangeCheck()) return;

            mode = selected;
            ExistingComponentHandlingPreferences.Save(preferenceKey, selected);
        }
    }
}
