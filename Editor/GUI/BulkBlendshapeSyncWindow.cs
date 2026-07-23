using System;
using System.Collections.Generic;
using System.Linq;
using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.windows
{
    public sealed class BulkBlendshapeSyncWindow : EditorWindow
    {
        private const string HandlingModePreferenceKey =
            "net.bekobeko.utilitytools.blendshapeSync.existingComponentHandling";
        private const string UIModePreferenceKey =
            "net.bekobeko.utilitytools.blendshapeSync.uiMode";
        private SkinnedMeshRenderer _referenceRenderer;
        private GameObject _targetRoot;
        private BlendshapeMappingModel _model;
        private string[] _referenceOptionLabels = Array.Empty<string>();
        private Vector2 _scrollPosition;
        private bool _showBulkSettings = true;
        private bool _showRendererSettings = true;
        private readonly HashSet<int> _expandedRenderers = new HashSet<int>();
        private readonly HashSet<string> _expandedBlendshapes = new HashSet<string>(StringComparer.Ordinal);
        private ExistingComponentHandlingMode _handlingMode;
        private ToolUIMode _uiMode;

        [MenuItem("Tools/KerBekoShop/UtilityTools/Bulk Blendshape Sync for MA")]
        private static void ShowWindow()
        {
            GetWindow<BulkBlendshapeSyncWindow>("Bulk Blendshape Sync");
        }

        private void OnEnable()
        {
            _handlingMode = ExistingComponentHandlingPreferences.Load(HandlingModePreferenceKey);
            _uiMode = ToolUIModeGUI.Load(UIModePreferenceKey);
        }

        private void OnGUI()
        {
            titleContent.text = EditorLocalization.Get("blendshapeSync.title");
            if (EditorLocalization.DrawLanguageSelector())
            {
                RefreshReferenceOptions();
                Repaint();
            }
            if (ToolUIModeGUI.Draw(UIModePreferenceKey, ref _uiMode) && _uiMode == ToolUIMode.Normal)
                EnableAllEntries();
            EditorGUILayout.Space();

            if (_uiMode == ToolUIMode.Advanced)
                ExistingComponentHandlingGUI.Draw(HandlingModePreferenceKey, ref _handlingMode);

            EditorGUI.BeginChangeCheck();
            _referenceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                EditorLocalization.Get("blendshapeSync.referenceMesh"),
                _referenceRenderer,
                typeof(SkinnedMeshRenderer),
                true);
            _targetRoot = (GameObject)EditorGUILayout.ObjectField(
                EditorLocalization.Get("blendshapeSync.outfitRoot"), _targetRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                _model = null;
                _expandedRenderers.Clear();
                _expandedBlendshapes.Clear();
                RefreshReferenceOptions();
            }

            using (new EditorGUI.DisabledScope(_referenceRenderer == null || _targetRoot == null))
            {
                if (GUILayout.Button(EditorLocalization.Get("blendshapeSync.detect"))) DetectBlendshapes();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            _showBulkSettings = EditorGUILayout.Foldout(
                _showBulkSettings,
                EditorLocalization.Get("blendshapeSync.bulkSection"),
                true,
                EditorStyles.foldoutHeader);
            if (_showBulkSettings && _model != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    if (_uiMode == ToolUIMode.Advanced) DrawAllEnabledToggle();
                    EditorGUILayout.LabelField(
                        EditorLocalization.Get("blendshapeSync.listHeader"),
                        EditorStyles.boldLabel);
                    foreach (var localBlendshape in _model.LocalBlendshapes)
                    {
                        DrawMapping(localBlendshape);
                    }
                }
            }
            else if (_showBulkSettings)
            {
                EditorGUILayout.HelpBox(
                    EditorLocalization.Get("blendshapeSync.detectFirst"),
                    MessageType.Info);
            }

            if (_uiMode == ToolUIMode.Advanced)
            {
                EditorGUILayout.Space();
                _showRendererSettings = EditorGUILayout.Foldout(
                    _showRendererSettings,
                    EditorLocalization.Get("blendshapeSync.rendererSection"),
                    true,
                    EditorStyles.foldoutHeader);
                if (_showRendererSettings && _model != null)
                {
                    DrawRendererSettings();
                }
                else if (_showRendererSettings)
                {
                    EditorGUILayout.HelpBox(
                        EditorLocalization.Get("blendshapeSync.detectFirst"),
                        MessageType.Info);
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(_model == null || _model.LocalBlendshapes.Count == 0))
            {
                if (GUILayout.Button(EditorLocalization.Get("blendshapeSync.add"))) AddComponents();
            }
        }

        private void DetectBlendshapes()
        {
            _model = BlendshapeCollector.CollectModel(_referenceRenderer, _targetRoot);
            _expandedRenderers.Clear();
            _expandedBlendshapes.Clear();
            RefreshReferenceOptions();
        }

        private void DrawMapping(string localBlendshape)
        {
            var state = _model.GetBulkState(localBlendshape);
            var expanded = _expandedBlendshapes.Contains(localBlendshape);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_uiMode == ToolUIMode.Advanced)
                {
                    expanded = GUILayout.Toggle(
                        expanded,
                        $"{localBlendshape} ({state.RendererCount})",
                        EditorStyles.foldout,
                        GUILayout.MinWidth(180f));

                    EditorGUI.showMixedValue = state.EnabledIsMixed;
                    EditorGUI.BeginChangeCheck();
                    var enabled = EditorGUILayout.ToggleLeft(
                        EditorLocalization.Get("common.enabled"),
                        state.Enabled,
                        GUILayout.Width(80f));
                    if (EditorGUI.EndChangeCheck()) _model.SetBulkEnabled(localBlendshape, enabled);
                    EditorGUI.showMixedValue = false;
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"{localBlendshape} ({state.RendererCount})",
                        GUILayout.MinWidth(180f));
                }

                var selectedIndex = GetReferenceOptionIndex(state.ReferenceBlendshape);

                EditorGUI.showMixedValue = state.ReferenceIsMixed;
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(state.RendererCount == 0))
                {
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, _referenceOptionLabels);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _model.SetBulkReference(
                        localBlendshape,
                        GetReferenceBlendshape(selectedIndex));
                }
                EditorGUI.showMixedValue = false;
            }

            if (_uiMode == ToolUIMode.Advanced)
            {
                if (expanded) _expandedBlendshapes.Add(localBlendshape);
                else _expandedBlendshapes.Remove(localBlendshape);

                if (expanded) DrawBlendshapeRendererMappings(localBlendshape);
            }
        }

        private void DrawAllEnabledToggle()
        {
            var hasEnabled = false;
            var hasDisabled = false;
            foreach (var localBlendshape in _model.LocalBlendshapes)
            {
                var state = _model.GetBulkState(localBlendshape);
                if (state.RendererCount == 0) continue;

                hasEnabled |= state.Enabled || state.EnabledIsMixed;
                hasDisabled |= !state.Enabled || state.EnabledIsMixed;
            }

            var isMixed = hasEnabled && hasDisabled;
            EditorGUI.showMixedValue = isMixed;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!hasEnabled && !hasDisabled))
            {
                var enabled = EditorGUILayout.ToggleLeft(
                    EditorLocalization.Get("common.allEnabled"),
                    isMixed ? false : hasEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var localBlendshape in _model.LocalBlendshapes)
                        _model.SetBulkEnabled(localBlendshape, enabled);
                }
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawBlendshapeRendererMappings(string localBlendshape)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var rendererEntry in _model.Renderers)
                {
                    if (!rendererEntry.Included || rendererEntry.Renderer == null ||
                        !rendererEntry.TryGetMapping(localBlendshape, out var mapping))
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            rendererEntry.Renderer.name,
                            GUILayout.MinWidth(180f));
                        mapping.Enabled = EditorGUILayout.ToggleLeft(
                            EditorLocalization.Get("common.enabled"),
                            mapping.Enabled,
                            GUILayout.Width(80f));
                        DrawReferencePopup(mapping);
                    }
                }
            }
        }

        private void RefreshReferenceOptions()
        {
            var names = _model?.ReferenceBlendshapes ?? Array.Empty<string>();
            _referenceOptionLabels = new string[names.Count + 1];
            _referenceOptionLabels[0] = EditorLocalization.Get("blendshapeSync.none");
            for (var index = 0; index < names.Count; index++)
                _referenceOptionLabels[index + 1] = names[index];
        }

        private void DrawRendererSettings()
        {
            if (_model.Renderers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    EditorLocalization.Get("blendshapeSync.noRenderers"),
                    MessageType.Info);
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var rendererEntry in _model.Renderers)
                {
                    DrawRendererEntry(rendererEntry);
                }
            }
        }

        private void DrawRendererEntry(BlendshapeRendererEntry rendererEntry)
        {
            if (rendererEntry.Renderer == null) return;

            var id = rendererEntry.Renderer.GetInstanceID();
            var expanded = _expandedRenderers.Contains(id);
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(
                    expanded,
                    rendererEntry.Renderer.name,
                    true);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        rendererEntry.Renderer,
                        typeof(SkinnedMeshRenderer),
                        true,
                        GUILayout.MinWidth(120f));
                }
                rendererEntry.Included = EditorGUILayout.ToggleLeft(
                    EditorLocalization.Get("blendshapeSync.includeRenderer"),
                    rendererEntry.Included,
                    GUILayout.Width(100f));
            }

            if (expanded) _expandedRenderers.Add(id);
            else _expandedRenderers.Remove(id);

            if (!expanded) return;
            using (new EditorGUI.DisabledScope(!rendererEntry.Included))
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var pair in rendererEntry.Mappings.OrderBy(
                             pair => pair.Key,
                             StringComparer.Ordinal))
                {
                    DrawRendererMapping(pair.Value);
                }
            }
        }

        private void DrawRendererMapping(RendererBlendshapeMapping mapping)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                mapping.Enabled = EditorGUILayout.ToggleLeft(
                    mapping.LocalBlendshape,
                    mapping.Enabled,
                    GUILayout.MinWidth(180f));

                DrawReferencePopup(mapping);
            }
        }

        private void DrawReferencePopup(RendererBlendshapeMapping mapping)
        {
            var selectedIndex = GetReferenceOptionIndex(mapping.ReferenceBlendshape);

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(selectedIndex, _referenceOptionLabels);
            if (EditorGUI.EndChangeCheck())
                mapping.ReferenceBlendshape = GetReferenceBlendshape(selectedIndex);
        }

        private int GetReferenceOptionIndex(string referenceBlendshape)
        {
            if (string.IsNullOrEmpty(referenceBlendshape) || _model == null) return 0;

            var names = _model.ReferenceBlendshapes;
            for (var index = 0; index < names.Count; index++)
            {
                if (string.Equals(names[index], referenceBlendshape, StringComparison.Ordinal))
                    return index + 1;
            }

            return 0;
        }

        private string GetReferenceBlendshape(int optionIndex)
        {
            var names = _model?.ReferenceBlendshapes;
            var nameIndex = optionIndex - 1;
            return names != null && nameIndex >= 0 && nameIndex < names.Count
                ? names[nameIndex]
                : null;
        }

        private void AddComponents()
        {
            var handlingMode = ToolWindowModeHelper.GetEffectiveHandlingMode(_uiMode, _handlingMode);
            var conflictSession = ConflictSessionFactory.CreateIfMerge(handlingMode);
            var result = BlendshapeSyncBuilder.Build(_model, handlingMode, conflictSession);
            if (result.Cancelled)
            {
                ResultDialogGUI.Show("blendshapeSync.title", "blendshapeSync.cancelled");
            }
            else if (result.AddedCount > 0 || result.UpdatedCount > 0 ||
                     result.ExistingComponentTargets.Count > 0 || result.UnchangedCount > 0)
            {
                var skippedNames = result.ExistingComponentTargets.Count == 0
                    ? EditorLocalization.Get("blendshapeSync.none")
                    : string.Join("\n", result.ExistingComponentTargets.Select(target => $"- {target.name}"));
                ResultDialogGUI.Show(
                    "blendshapeSync.title",
                    "blendshapeSync.result",
                    result.AddedCount,
                    result.UpdatedCount,
                    result.ExistingComponentTargets.Count,
                    result.UnchangedCount,
                    skippedNames);
            }
            else
            {
                ResultDialogGUI.Show("blendshapeSync.title", "blendshapeSync.noMappings");
            }
        }

        private void EnableAllEntries()
        {
            if (_model == null) return;
            foreach (var renderer in _model.Renderers)
            {
                renderer.Included = true;
                foreach (var mapping in renderer.Mappings.Values) mapping.Enabled = true;
            }
        }
    }
}
