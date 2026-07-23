using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.windows
{
    public sealed class ConflictResolutionDialog : EditorWindow
    {
        private ConflictInfo _conflict;
        private bool _applyToAll;
        private ConflictResolutionDecision _decision =
            new ConflictResolutionDecision(ConflictResolutionAction.Cancel, false);

        public static ConflictResolutionDecision Show(ConflictInfo conflict)
        {
            var window = CreateInstance<ConflictResolutionDialog>();
            window.titleContent = new GUIContent(EditorLocalization.Get("conflict.title"));
            window._conflict = conflict ?? new ConflictInfo(string.Empty, string.Empty, string.Empty);
            window.minSize = new Vector2(460f, 240f);
            window.maxSize = new Vector2(700f, 420f);
            window.ShowModalUtility();
            return window._decision;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                EditorLocalization.Get("conflict.message"),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                EditorLocalization.Get("conflict.subject"),
                _conflict?.Subject ?? string.Empty);
            DrawValue(EditorLocalization.Get("conflict.existing"), _conflict?.ExistingValue);
            DrawValue(EditorLocalization.Get("conflict.generated"), _conflict?.GeneratedValue);
            GUILayout.FlexibleSpace();
            _applyToAll = EditorGUILayout.ToggleLeft(
                EditorLocalization.Get("conflict.applyToAll"),
                _applyToAll);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(EditorLocalization.Get("conflict.skip")))
                    Complete(ConflictResolutionAction.Skip);
                if (GUILayout.Button(EditorLocalization.Get("conflict.overwrite")))
                    Complete(ConflictResolutionAction.Overwrite);
                if (GUILayout.Button(EditorLocalization.Get("conflict.cancel")))
                    Complete(ConflictResolutionAction.Cancel);
            }
        }

        private static void DrawValue(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
                EditorGUILayout.SelectableLabel(
                    value ?? string.Empty,
                    EditorStyles.textArea,
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));
        }

        private void Complete(ConflictResolutionAction action)
        {
            _decision = new ConflictResolutionDecision(action, _applyToAll);
            Close();
        }
    }
}
