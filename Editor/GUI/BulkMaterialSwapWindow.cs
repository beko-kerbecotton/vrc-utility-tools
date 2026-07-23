using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace net.bekobeko.utilitytools.windows
{
    public sealed class BulkMaterialSwapWindow : EditorWindow
    {
        private const string HandlingModePreferenceKey =
            "net.bekobeko.utilitytools.materialSwap.existingComponentHandling";
        private const string UIModePreferenceKey =
            "net.bekobeko.utilitytools.materialSwap.uiMode";
        private const string AttachmentModePreferenceKey =
            "net.bekobeko.utilitytools.materialSwap.attachmentMode";
        private const string QuickSwapModePreferenceKey =
            "net.bekobeko.utilitytools.materialSwap.quickSwapMode";
        private const string MenuContainerName = "Material Swap";
        private GameObject _componentTarget;
        private GameObject _avatarRoot;
        private GameObject _targetRoot;
        private MenuItemAttachmentMode _attachmentMode;
        private string _toggleObjectName = "Toggle";
        private readonly List<MaterialSwapEntry> _entries = new List<MaterialSwapEntry>();
        private readonly Dictionary<Material, List<Material>> _candidateCache =
            new Dictionary<Material, List<Material>>();
        private ReorderableList _swapList;
        private Vector2 _scrollPosition;
        private QuickSwapMode _mode = QuickSwapMode.None;
        private ExistingComponentHandlingMode _handlingMode;
        private ToolUIMode _uiMode;
        private readonly MenuAncestorWarningCache _ancestorWarningCache = new MenuAncestorWarningCache();
        private GUIStyle _pathLabelStyle;

        [MenuItem("Tools/KerBekoShop/UtilityTools/Bulk Material Swap for MA")]
        private static void ShowWindow()
        {
            GetWindow<BulkMaterialSwapWindow>("Bulk Material Swap");
        }

        private void OnEnable()
        {
            _ancestorWarningCache.Enable();
            _handlingMode = ExistingComponentHandlingPreferences.Load(HandlingModePreferenceKey);
            _uiMode = ToolUIModeGUI.Load(UIModePreferenceKey);
            _attachmentMode = MenuItemAttachmentGUI.LoadMode(AttachmentModePreferenceKey);
            _mode = LoadQuickSwapMode();
            _swapList = new ReorderableList(_entries, typeof(MaterialSwapEntry), false, true, false, true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, EditorLocalization.Get("materialSwap.listHeader")),
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawElementCallback = DrawEntry
            };
        }

        private void OnDisable()
        {
            _ancestorWarningCache.Disable();
        }

        private void OnGUI()
        {
            if (_pathLabelStyle == null)
            {
                _pathLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 8,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            titleContent.text = EditorLocalization.Get("materialSwap.title");
            if (EditorLocalization.DrawLanguageSelector()) Repaint();
            if (ToolUIModeGUI.Draw(UIModePreferenceKey, ref _uiMode) && _uiMode == ToolUIMode.Normal)
                EnableAllEntries();
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_uiMode == ToolUIMode.Advanced)
                ExistingComponentHandlingGUI.Draw(HandlingModePreferenceKey, ref _handlingMode);
            EditorGUI.BeginChangeCheck();
            var mode = (QuickSwapMode)EditorGUILayout.Popup(
                EditorLocalization.Get("materialSwap.mode"),
                (int)_mode,
                new[]
                {
                    EditorLocalization.Get("materialSwap.modeNormal"),
                    EditorLocalization.Get("materialSwap.modeSameDirectory"),
                    EditorLocalization.Get("materialSwap.modeSiblingDirectory")
                });
            if (EditorGUI.EndChangeCheck())
            {
                ChangeMode(mode);
            }

            if (_uiMode == ToolUIMode.Advanced)
            {
                MenuItemAttachmentGUI.Draw(
                    "materialSwap.componentTarget",
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
                EditorLocalization.Get("materialSwap.rendererRoot"),
                _targetRoot,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                _entries.Clear();
                _candidateCache.Clear();
                _swapList.index = -1;
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_targetRoot == null))
            {
                if (GUILayout.Button(EditorLocalization.Get("materialSwap.collect")))
                {
                    _entries.Clear();
                    _entries.AddRange(MaterialSwapCollector.Collect(_targetRoot));
                    RebuildCandidateCache();
                    _swapList.index = -1;
                }
            }

            RemoveDestroyedEntries();
            if (_uiMode == ToolUIMode.Advanced) DrawAllEnabledToggle();
            _swapList.DoLayoutList();

            using (new EditorGUI.DisabledScope(
                       !MenuItemAttachmentGUI.IsValid(
                           ToolWindowModeHelper.GetEffectiveAttachmentMode(_uiMode, _attachmentMode),
                           _componentTarget,
                           _avatarRoot,
                           _toggleObjectName) ||
                       _targetRoot == null || _entries.Count == 0))
            {
                if (GUILayout.Button(EditorLocalization.Get("materialSwap.add"))) AddComponent();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _entries.Count) return;

            var entry = _entries[index];
            if (entry == null || entry.Original == null) return;
            const float spacing = 4f;
            var enabledWidth = _uiMode == ToolUIMode.Advanced ? 80f : 0f;
            var spacingCount = _uiMode == ToolUIMode.Advanced ? 2f : 1f;
            var width = (rect.width - enabledWidth - spacing * spacingCount) / 2f;
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            if (_uiMode == ToolUIMode.Advanced)
            {
                entry.Enabled = EditorGUI.ToggleLeft(
                    new Rect(rect.x, rect.y, enabledWidth, rect.height),
                    EditorLocalization.Get("common.enabled"),
                    entry.Enabled);
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.ObjectField(
                    new Rect(
                        rect.x + enabledWidth + (_uiMode == ToolUIMode.Advanced ? spacing : 0f),
                        rect.y,
                        width,
                        rect.height),
                    entry.Original,
                    typeof(Material),
                    false);
            }

            var replacementRect = new Rect(
                rect.x + enabledWidth + width + spacing * spacingCount,
                rect.y,
                width,
                rect.height);
            using (new EditorGUI.DisabledScope(!entry.Enabled))
            {
                if (_mode == QuickSwapMode.None)
                {
                    entry.Replacement = (Material)EditorGUI.ObjectField(
                        replacementRect, entry.Replacement, typeof(Material), false);
                }
                else
                {
                    DrawCandidateSelector(replacementRect, entry);
                }
            }
        }

        private void DrawAllEnabledToggle()
        {
            var hasEnabled = _entries.Exists(entry => entry != null && entry.Enabled);
            var hasDisabled = _entries.Exists(entry => entry != null && !entry.Enabled);
            var isMixed = hasEnabled && hasDisabled;

            EditorGUI.showMixedValue = isMixed;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(_entries.Count == 0))
            {
                var enabled = EditorGUILayout.ToggleLeft(
                    EditorLocalization.Get("common.allEnabled"),
                    isMixed ? false : hasEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var entry in _entries)
                    {
                        if (entry != null) entry.Enabled = enabled;
                    }
                }
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawCandidateSelector(Rect rect, MaterialSwapEntry entry)
        {
            if (!_candidateCache.TryGetValue(entry.Original, out var candidates))
                candidates = new List<Material>();

            const float buttonWidth = 24f;
            var fieldRect = new Rect(rect.x, rect.y, rect.width - buttonWidth * 2f, rect.height);
            var leftRect = new Rect(fieldRect.xMax, rect.y, buttonWidth, rect.height);
            var rightRect = new Rect(leftRect.xMax, rect.y, buttonWidth, rect.height);

            DrawReplacementField(fieldRect, entry.Replacement, candidates);

            var currentIndex = entry.Replacement == null ? -1 : candidates.IndexOf(entry.Replacement);
            var canMoveLeft = candidates.Count > 0 && (currentIndex == -1 || currentIndex > 0);
            var canMoveRight = candidates.Count > 0 && currentIndex < candidates.Count - 1;

            using (new EditorGUI.DisabledScope(!canMoveLeft))
            {
                if (GUI.Button(leftRect, "<", EditorStyles.miniButtonLeft))
                    entry.Replacement = currentIndex == -1 ? candidates[candidates.Count - 1] : candidates[currentIndex - 1];
            }
            using (new EditorGUI.DisabledScope(!canMoveRight))
            {
                if (GUI.Button(rightRect, ">", EditorStyles.miniButtonRight))
                    entry.Replacement = currentIndex == -1 ? candidates[0] : candidates[currentIndex + 1];
            }
        }

        private void DrawReplacementField(Rect rect, Material material, IReadOnlyList<Material> candidates)
        {
            var pathPrefix = _mode == QuickSwapMode.SiblingDirectory
                ? GetUniqueFolderSuffix(material, candidates)
                : null;
            if (GUI.Button(rect, GUIContent.none, EditorStyles.objectField) && material != null)
                EditorGUIUtility.PingObject(material);

            var content = EditorGUIUtility.ObjectContent(material, typeof(Material));
            const float padding = 3f;
            const float iconSize = 16f;
            var contentX = rect.x + padding;
            if (content.image != null)
            {
                var iconRect = new Rect(
                    contentX,
                    rect.y + (rect.height - iconSize) / 2f,
                    iconSize,
                    iconSize);
                GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit);
                contentX = iconRect.xMax + 2f;
            }

            if (!string.IsNullOrEmpty(pathPrefix))
            {
                var prefixWidth = _pathLabelStyle.CalcSize(new GUIContent(pathPrefix)).x;
                GUI.Label(
                    new Rect(contentX, rect.y, prefixWidth, rect.height),
                    pathPrefix,
                    _pathLabelStyle);
                contentX += prefixWidth;
            }

            var textRect = new Rect(
                contentX,
                rect.y,
                Mathf.Max(0f, rect.xMax - padding - contentX),
                rect.height);
            GUI.Label(textRect, content.text, EditorStyles.label);
        }

        private static string GetUniqueFolderSuffix(Material material, IReadOnlyList<Material> candidates)
        {
            if (material == null || candidates == null) return null;
            var assetPath = AssetDatabase.GetAssetPath(material);
            var folderPath = GetFolderPath(assetPath);
            if (string.IsNullOrEmpty(folderPath)) return null;

            var sameNameFolders = new List<string>();
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.name != material.name) continue;
                var candidateFolder = GetFolderPath(AssetDatabase.GetAssetPath(candidate));
                if (!string.IsNullOrEmpty(candidateFolder)) sameNameFolders.Add(candidateFolder);
            }

            var segments = folderPath.Split('/');
            for (var count = 1; count <= segments.Length; count++)
            {
                var suffix = string.Join("/", segments, segments.Length - count, count);
                var unique = true;
                foreach (var otherFolder in sameNameFolders)
                {
                    if (otherFolder == folderPath) continue;
                    if (!otherFolder.EndsWith(suffix, StringComparison.Ordinal)) continue;
                    unique = false;
                    break;
                }
                if (unique) return suffix + "/";
            }

            return null;
        }

        private static string GetFolderPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var separator = assetPath.LastIndexOf('/');
            return separator <= 0 ? null : assetPath.Substring(0, separator);
        }

        private void ChangeMode(QuickSwapMode mode)
        {
            _mode = mode;
            EditorPrefs.SetInt(QuickSwapModePreferenceKey, (int)_mode);
            foreach (var entry in _entries) entry.Replacement = null;
            RebuildCandidateCache();
        }

        private void RebuildCandidateCache()
        {
            _candidateCache.Clear();
            if (_mode == QuickSwapMode.None) return;
            foreach (var entry in _entries)
            {
                if (entry.Original == null) continue;
                _candidateCache[entry.Original] =
                    MaterialSwapCandidateFinder.BuildCandidateList(_mode, entry.Original);
            }
        }

        private void RemoveDestroyedEntries()
        {
            if (_entries.RemoveAll(entry => entry == null || entry.Original == null) == 0) return;

            RebuildCandidateCache();
            _swapList.index = -1;
        }

        private void AddComponent()
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add MA Material Swap");
            var attachmentResult = MenuItemAttachmentUtility.ResolveComponentTarget(
                ToolWindowModeHelper.GetEffectiveAttachmentMode(_uiMode, _attachmentMode),
                _componentTarget,
                _avatarRoot,
                MenuContainerName,
                _toggleObjectName);
            var handlingMode = ToolWindowModeHelper.GetEffectiveHandlingMode(_uiMode, _handlingMode);
            var conflictSession = ConflictSessionFactory.CreateIfMerge(handlingMode);
            var status = MaterialSwapBuilder.Build(
                attachmentResult.ComponentTarget,
                _targetRoot,
                _entries,
                _mode,
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
            ResultDialogGUI.ShowBuildResult("materialSwap.title", "materialSwap", status, attachmentResult);
        }

        private void EnableAllEntries()
        {
            foreach (var entry in _entries)
            {
                if (entry != null) entry.Enabled = true;
            }
        }

        private static QuickSwapMode LoadQuickSwapMode()
        {
            var stored = EditorPrefs.GetInt(QuickSwapModePreferenceKey, (int)QuickSwapMode.None);
            return Enum.IsDefined(typeof(QuickSwapMode), stored)
                ? (QuickSwapMode)stored
                : QuickSwapMode.None;
        }
    }
}
