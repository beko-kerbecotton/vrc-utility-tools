using System;
using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.windows
{
    public sealed class MenuAncestorWarningCache
    {
        private GameObject _target;
        private bool _hasAncestorMenuContainer;
        private bool _isValid;
        private bool _isSubscribed;

        public void Enable()
        {
            if (!_isSubscribed)
            {
                EditorApplication.hierarchyChanged += Invalidate;
                _isSubscribed = true;
            }

            Invalidate();
        }

        public void Disable()
        {
            if (_isSubscribed)
            {
                EditorApplication.hierarchyChanged -= Invalidate;
                _isSubscribed = false;
            }

            Invalidate();
        }

        public bool HasAncestorMenuContainer(GameObject target)
        {
            if (_isValid && _target == target) return _hasAncestorMenuContainer;

            _target = target;
            _hasAncestorMenuContainer = MenuItemAttachmentUtility.HasAncestorMenuContainer(target);
            _isValid = true;
            return _hasAncestorMenuContainer;
        }

        private void Invalidate()
        {
            _isValid = false;
        }
    }

    public static class MenuItemAttachmentGUI
    {
        public static void Draw(
            string componentTargetKey,
            string preferenceKey,
            ref MenuItemAttachmentMode mode,
            ref GameObject componentTarget,
            ref GameObject avatarRoot,
            ref string toggleObjectName,
            MenuAncestorWarningCache ancestorWarningCache)
        {
            var selectedMode = (MenuItemAttachmentMode)EditorGUILayout.Popup(
                EditorLocalization.Get("menuAttach.mode"),
                (int)mode,
                new[]
                {
                    EditorLocalization.Get("menuAttach.direct"),
                    EditorLocalization.Get("menuAttach.toggle"),
                    EditorLocalization.Get("menuAttach.hierarchy")
                });
            if (selectedMode != mode)
            {
                mode = selectedMode;
                SaveMode(preferenceKey, mode);
            }

            if (mode == MenuItemAttachmentMode.CreateMenuHierarchy)
            {
                DrawHierarchyFields(ref avatarRoot, ref toggleObjectName);
                return;
            }

            componentTarget = (GameObject)EditorGUILayout.ObjectField(
                EditorLocalization.Get(componentTargetKey),
                componentTarget,
                typeof(GameObject),
                true);

            if (mode == MenuItemAttachmentMode.AttachToggle && componentTarget != null &&
                !(ancestorWarningCache?.HasAncestorMenuContainer(componentTarget) ??
                  MenuItemAttachmentUtility.HasAncestorMenuContainer(componentTarget)))
                EditorGUILayout.HelpBox(
                    EditorLocalization.Get("menuAttach.noAncestorWarning"),
                    MessageType.Warning);
        }

        public static bool IsValid(
            MenuItemAttachmentMode mode,
            GameObject componentTarget,
            GameObject avatarRoot,
            string toggleObjectName)
        {
            return mode == MenuItemAttachmentMode.CreateMenuHierarchy
                ? avatarRoot != null && !string.IsNullOrWhiteSpace(toggleObjectName)
                : componentTarget != null;
        }

        public static MenuItemAttachmentMode LoadMode(string preferenceKey)
        {
            if (string.IsNullOrEmpty(preferenceKey)) return MenuItemAttachmentMode.Direct;
            var stored = EditorPrefs.GetInt(preferenceKey, (int)MenuItemAttachmentMode.Direct);
            return Enum.IsDefined(typeof(MenuItemAttachmentMode), stored)
                ? (MenuItemAttachmentMode)stored
                : MenuItemAttachmentMode.Direct;
        }

        private static void SaveMode(string preferenceKey, MenuItemAttachmentMode mode)
        {
            if (string.IsNullOrEmpty(preferenceKey)) return;
            var value = Enum.IsDefined(typeof(MenuItemAttachmentMode), mode)
                ? mode
                : MenuItemAttachmentMode.Direct;
            EditorPrefs.SetInt(preferenceKey, (int)value);
        }

        public static void DrawHierarchyFields(
            ref GameObject avatarRoot,
            ref string toggleObjectName)
        {
            avatarRoot = (GameObject)EditorGUILayout.ObjectField(
                EditorLocalization.Get("menuAttach.avatarRoot"),
                avatarRoot,
                typeof(GameObject),
                true);
            toggleObjectName = EditorGUILayout.TextField(
                EditorLocalization.Get("menuAttach.toggleObjectName"),
                toggleObjectName);

            if (avatarRoot == null || string.IsNullOrWhiteSpace(toggleObjectName))
                EditorGUILayout.HelpBox(
                    EditorLocalization.Get("menuAttach.hierarchyRequired"),
                    MessageType.Info);
        }
    }
}
