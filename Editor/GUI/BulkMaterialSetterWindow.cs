using System.Collections.Generic;
using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace net.bekobeko.utilitytools.windows
{
    public sealed class BulkMaterialSetterWindow : EditorWindow
    {
        private const string HandlingModePreferenceKey =
            "net.bekobeko.utilitytools.materialSetter.existingComponentHandling";
        private const string UIModePreferenceKey =
            "net.bekobeko.utilitytools.materialSetter.uiMode";
        private const string AttachmentModePreferenceKey =
            "net.bekobeko.utilitytools.materialSetter.attachmentMode";
        private const string MenuContainerName = "Material Setter";
        private GameObject _componentTarget;
        private GameObject _avatarRoot;
        private GameObject _targetRoot;
        private MenuItemAttachmentMode _attachmentMode;
        private string _toggleObjectName = "Toggle";
        private MaterialMappingModel _model;
        private readonly List<Material> _bulkMaterials = new List<Material>();
        private ReorderableList _replacementList;
        private Vector2 _scrollPosition;
        private bool _showBulkSettings = true;
        private bool _showRendererSettings = true;
        private readonly HashSet<int> _expandedRenderers = new HashSet<int>();
        private readonly MenuAncestorWarningCache _ancestorWarningCache = new MenuAncestorWarningCache();
        private ExistingComponentHandlingMode _handlingMode;
        private ToolUIMode _uiMode;

        [MenuItem("Tools/KerBekoShop/UtilityTools/Bulk Material Setter for MA")]
        private static void ShowWindow()
        {
            GetWindow<BulkMaterialSetterWindow>("Bulk Material Setter");
        }

        private void OnEnable()
        {
            _ancestorWarningCache.Enable();
            _handlingMode = ExistingComponentHandlingPreferences.Load(HandlingModePreferenceKey);
            _uiMode = ToolUIModeGUI.Load(UIModePreferenceKey);
            _attachmentMode = MenuItemAttachmentGUI.LoadMode(AttachmentModePreferenceKey);
            _replacementList = new ReorderableList(_bulkMaterials, typeof(Material), false, true, false, false)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(
                    rect,
                    EditorLocalization.Get(_uiMode == ToolUIMode.Advanced
                        ? "materialSetter.listHeader"
                        : "materialSetter.listHeaderNormal"))
            };

            _replacementList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            _replacementList.drawElementCallback = DrawReplacement;
        }

        private void OnDisable()
        {
            _ancestorWarningCache.Disable();
        }

        private void OnGUI()
        {
            titleContent.text = EditorLocalization.Get("materialSetter.title");
            if (EditorLocalization.DrawLanguageSelector()) Repaint();
            if (ToolUIModeGUI.Draw(UIModePreferenceKey, ref _uiMode) && _uiMode == ToolUIMode.Normal)
                EnableAllEntries();
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_uiMode == ToolUIMode.Advanced)
            {
                ExistingComponentHandlingGUI.Draw(HandlingModePreferenceKey, ref _handlingMode);
                MenuItemAttachmentGUI.Draw(
                    "materialSetter.componentTarget",
                    AttachmentModePreferenceKey,
                    ref _attachmentMode,
                    ref _componentTarget,
                    ref _avatarRoot,
                    ref _toggleObjectName,
                    _ancestorWarningCache);
            }
            else
            {
                MenuItemAttachmentGUI.DrawHierarchyFields(ref _avatarRoot, ref _toggleObjectName);
            }
            EditorGUI.BeginChangeCheck();
            _targetRoot = (GameObject)EditorGUILayout.ObjectField(
                EditorLocalization.Get("materialSetter.rendererRoot"), _targetRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                _model = null;
                _bulkMaterials.Clear();
                _expandedRenderers.Clear();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_targetRoot == null))
            {
                if (GUILayout.Button(EditorLocalization.Get("materialSetter.collect"))) CollectMaterials();
            }

            RemoveDestroyedBulkMaterials();
            _showBulkSettings = EditorGUILayout.Foldout(
                _showBulkSettings,
                EditorLocalization.Get("materialSetter.bulkSection"),
                true,
                EditorStyles.foldoutHeader);
            if (_showBulkSettings && _model != null)
            {
                if (_uiMode == ToolUIMode.Advanced) DrawAllEnabledToggle();
                _replacementList?.DoLayoutList();
            }

            if (_uiMode == ToolUIMode.Advanced)
            {
                EditorGUILayout.Space();
                _showRendererSettings = EditorGUILayout.Foldout(
                    _showRendererSettings,
                    EditorLocalization.Get("materialSetter.rendererSection"),
                    true,
                    EditorStyles.foldoutHeader);
                if (_showRendererSettings && _model != null) DrawRendererSettings();
            }

            using (new EditorGUI.DisabledScope(
                       !MenuItemAttachmentGUI.IsValid(
                           ToolWindowModeHelper.GetEffectiveAttachmentMode(_uiMode, _attachmentMode),
                           _componentTarget,
                           _avatarRoot,
                           _toggleObjectName) ||
                       _model == null))
            {
                if (GUILayout.Button(EditorLocalization.Get("materialSetter.add"))) AddComponent();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawReplacement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (_model == null || index < 0 || index >= _bulkMaterials.Count) return;

            var material = _bulkMaterials[index];
            if (material == null) return;
            var state = _model.GetBulkState(material);
            var spacing = 4f;
            var enabledWidth = _uiMode == ToolUIMode.Advanced ? 80f : 0f;
            var spacingCount = _uiMode == ToolUIMode.Advanced ? 2f : 1f;
            var width = (rect.width - enabledWidth - spacing * spacingCount) / 2f;
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            if (_uiMode == ToolUIMode.Advanced)
            {
                EditorGUI.showMixedValue = state.EnabledIsMixed;
                EditorGUI.BeginChangeCheck();
                var enabled = EditorGUI.ToggleLeft(
                    new Rect(rect.x, rect.y, enabledWidth, rect.height),
                    EditorLocalization.Get("common.enabled"),
                    state.Enabled);
                if (EditorGUI.EndChangeCheck()) _model.SetBulkEnabled(material, enabled);
                EditorGUI.showMixedValue = false;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.ObjectField(
                    new Rect(
                        rect.x + enabledWidth + (_uiMode == ToolUIMode.Advanced ? spacing : 0f),
                        rect.y,
                        width,
                        rect.height),
                    material,
                    typeof(Material),
                    false);
            }

            EditorGUI.showMixedValue = state.ReplacementIsMixed;
            EditorGUI.BeginChangeCheck();
            var replacement = (Material)EditorGUI.ObjectField(
                new Rect(rect.x + enabledWidth + width + spacing * spacingCount, rect.y, width, rect.height),
                state.Replacement,
                typeof(Material),
                false);
            if (EditorGUI.EndChangeCheck()) _model.SetBulkReplacement(material, replacement);
            EditorGUI.showMixedValue = false;
        }

        private void DrawAllEnabledToggle()
        {
            var hasEnabled = false;
            var hasDisabled = false;
            foreach (var material in _bulkMaterials)
            {
                if (material == null) continue;

                var state = _model.GetBulkState(material);
                if (state.SlotCount == 0) continue;

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
                    foreach (var material in _bulkMaterials)
                    {
                        if (material != null) _model.SetBulkEnabled(material, enabled);
                    }
                }
            }
            EditorGUI.showMixedValue = false;
        }

        private void CollectMaterials()
        {
            _model = MaterialCollector.CollectModel(_targetRoot);
            _bulkMaterials.Clear();
            _bulkMaterials.AddRange(_model.OriginalMaterials);
            _expandedRenderers.Clear();
            _replacementList.index = -1;
        }

        private void RemoveDestroyedBulkMaterials()
        {
            if (_model == null) return;

            _bulkMaterials.RemoveAll(material => material == null);
            if (_replacementList.index >= _bulkMaterials.Count) _replacementList.index = -1;
        }

        private void DrawRendererSettings()
        {
            foreach (var entry in _model.Renderers)
            {
                if (entry.Renderer == null) continue;
                var id = entry.Renderer.GetInstanceID();
                var expanded = _expandedRenderers.Contains(id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded, entry.Renderer.name, true);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(entry.Renderer, typeof(Renderer), true);
                    entry.Included = EditorGUILayout.ToggleLeft(
                        EditorLocalization.Get("materialSetter.includeRenderer"), entry.Included, GUILayout.Width(100f));
                }
                if (expanded) _expandedRenderers.Add(id); else _expandedRenderers.Remove(id);
                if (!expanded) continue;

                using (new EditorGUI.DisabledScope(!entry.Included))
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var slot in entry.Slots) DrawSlot(slot);
                }
            }
        }

        private static void DrawSlot(MaterialSlotMapping slot)
        {
            if (slot == null || slot.Original == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                var label = $"{slot.Original.name} [{slot.MaterialIndex}]";
                slot.Enabled = EditorGUILayout.ToggleLeft(label, slot.Enabled, GUILayout.MinWidth(180f));
                slot.Replacement = (Material)EditorGUILayout.ObjectField(
                    slot.Replacement, typeof(Material), false);
            }
        }

        private void AddComponent()
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add MA Material Setter");
            var attachmentResult = MenuItemAttachmentUtility.ResolveComponentTarget(
                ToolWindowModeHelper.GetEffectiveAttachmentMode(_uiMode, _attachmentMode),
                _componentTarget,
                _avatarRoot,
                MenuContainerName,
                _toggleObjectName);
            var handlingMode = ToolWindowModeHelper.GetEffectiveHandlingMode(_uiMode, _handlingMode);
            var conflictSession = ConflictSessionFactory.CreateIfMerge(handlingMode);
            var status = MaterialSetterBuilder.Build(
                attachmentResult.ComponentTarget,
                _model,
                handlingMode,
                conflictSession);
            if (ToolWindowModeHelper.ShouldRevertAttachment(status))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                attachmentResult = null;
            }
            else
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
            ResultDialogGUI.ShowBuildResult("materialSetter.title", "materialSetter", status, attachmentResult);
        }

        private void EnableAllEntries()
        {
            if (_model == null) return;
            foreach (var renderer in _model.Renderers)
            {
                renderer.Included = true;
                foreach (var slot in renderer.Slots) slot.Enabled = true;
            }
        }
    }
}
