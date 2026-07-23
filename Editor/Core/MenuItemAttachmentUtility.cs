using System;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public enum MenuItemAttachmentMode
    {
        Direct,
        AttachToggle,
        CreateMenuHierarchy
    }

    public sealed class MenuItemAttachmentResult
    {
        public GameObject ComponentTarget { get; internal set; }
        public int CreatedGameObjectCount { get; internal set; }
        public int AddedMenuItemCount { get; internal set; }
        public int AddedMenuInstallerCount { get; internal set; }
        public bool HasAdditions => CreatedGameObjectCount > 0 ||
                                    AddedMenuItemCount > 0 ||
                                    AddedMenuInstallerCount > 0;
    }

    public static class MenuItemAttachmentUtility
    {
        public static bool HasAncestorMenuContainer(GameObject target)
        {
            if (target == null) return false;

            for (var current = target.transform.parent; current != null; current = current.parent)
            {
                if (current.GetComponent<ModularAvatarMenuInstaller>() != null) return true;

                var menuItem = current.GetComponent<ModularAvatarMenuItem>();
                if (menuItem != null &&
                    menuItem.PortableControl.Type == PortableControlType.SubMenu &&
                    menuItem.MenuSource == SubmenuSource.Children)
                    return true;
            }

            return false;
        }

        public static MenuItemAttachmentResult ResolveComponentTarget(
            MenuItemAttachmentMode mode,
            GameObject selectedTarget,
            GameObject avatarRoot,
            string menuContainerName,
            string toggleObjectName)
        {
            var result = new MenuItemAttachmentResult();
            switch (mode)
            {
                case MenuItemAttachmentMode.Direct:
                    result.ComponentTarget = selectedTarget;
                    break;
                case MenuItemAttachmentMode.AttachToggle:
                    if (selectedTarget != null) AttachToggle(selectedTarget, result);
                    result.ComponentTarget = selectedTarget;
                    break;
                case MenuItemAttachmentMode.CreateMenuHierarchy:
                    result.ComponentTarget = CreateOrReuseMenuHierarchy(
                        avatarRoot, menuContainerName, toggleObjectName, result);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            return result;
        }

        private static GameObject CreateOrReuseMenuHierarchy(
            GameObject avatarRoot,
            string menuContainerName,
            string toggleObjectName,
            MenuItemAttachmentResult result)
        {
            if (avatarRoot == null || string.IsNullOrWhiteSpace(menuContainerName) ||
                string.IsNullOrWhiteSpace(toggleObjectName))
                return null;

            var menuObject = FindDirectChildWithMenuType(
                avatarRoot.transform,
                menuContainerName,
                PortableControlType.SubMenu,
                requireChildrenMenuSource: true);
            if (menuObject == null)
            {
                menuObject = CreateChild(avatarRoot.transform, menuContainerName, result);
                ConfigureNewSubMenu(menuObject, result);
            }
            EnsureMenuInstaller(menuObject, result);

            var toggleObject = FindDirectChildWithMenuType(
                menuObject.transform,
                toggleObjectName,
                PortableControlType.Toggle,
                requireChildrenMenuSource: false);
            if (toggleObject == null)
            {
                toggleObject = CreateChild(menuObject.transform, toggleObjectName, result);
                ConfigureNewToggle(toggleObject, result);
            }
            return toggleObject;
        }

        private static GameObject FindDirectChildWithMenuType(
            Transform parent,
            string objectName,
            PortableControlType menuType,
            bool requireChildrenMenuSource)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (!string.Equals(child.name, objectName, StringComparison.Ordinal)) continue;

                var menuItem = child.GetComponent<ModularAvatarMenuItem>();
                if (menuItem != null &&
                    menuItem.PortableControl.Type == menuType &&
                    (!requireChildrenMenuSource || menuItem.MenuSource == SubmenuSource.Children))
                    return child.gameObject;
            }

            return null;
        }

        private static GameObject CreateChild(
            Transform parent,
            string objectName,
            MenuItemAttachmentResult result)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(child, $"Create {objectName}");
            result.CreatedGameObjectCount++;
            return child;
        }

        private static void AttachToggle(GameObject target, MenuItemAttachmentResult result)
        {
            var existing = target.GetComponent<ModularAvatarMenuItem>();
            if (existing != null) return;

            ConfigureNewToggle(target, result);
        }

        private static void ConfigureNewSubMenu(GameObject target, MenuItemAttachmentResult result)
        {
            var menuItem = EnsureMenuItem(target, result);
            Undo.RecordObject(menuItem, "Configure MA Sub Menu");
            menuItem.PortableControl.Type = PortableControlType.SubMenu;
            menuItem.PortableControl.Parameter = string.Empty;
            menuItem.PortableControl.Value = 1f;
            menuItem.PortableControl.VRChatSubMenu = null;
            menuItem.MenuSource = SubmenuSource.Children;
            menuItem.menuSource_otherObjectChildren = null;
            menuItem.label = string.Empty;
            menuItem.isDefault = false;
            menuItem.isSaved = true;
            menuItem.isSynced = true;
            menuItem.automaticValue = true;
            EditorUtility.SetDirty(menuItem);
        }

        private static void ConfigureNewToggle(GameObject target, MenuItemAttachmentResult result)
        {
            var menuItem = EnsureMenuItem(target, result);
            Undo.RecordObject(menuItem, "Configure MA Menu Toggle");
            menuItem.PortableControl.Type = PortableControlType.Toggle;
            menuItem.PortableControl.Parameter = string.Empty;
            menuItem.PortableControl.Value = 1f;
            menuItem.PortableControl.VRChatSubMenu = null;
            menuItem.label = string.Empty;
            menuItem.isDefault = false;
            menuItem.isSaved = true;
            menuItem.isSynced = true;
            menuItem.automaticValue = true;
            EditorUtility.SetDirty(menuItem);
        }

        private static ModularAvatarMenuItem EnsureMenuItem(
            GameObject target,
            MenuItemAttachmentResult result)
        {
            var existing = target.GetComponent<ModularAvatarMenuItem>();
            if (existing != null) return existing;

            result.AddedMenuItemCount++;
            return Undo.AddComponent<ModularAvatarMenuItem>(target);
        }

        private static ModularAvatarMenuInstaller EnsureMenuInstaller(
            GameObject target,
            MenuItemAttachmentResult result)
        {
            var existing = target.GetComponent<ModularAvatarMenuInstaller>();
            if (existing != null) return existing;

            result.AddedMenuInstallerCount++;
            return Undo.AddComponent<ModularAvatarMenuInstaller>(target);
        }
    }
}
