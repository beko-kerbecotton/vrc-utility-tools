using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class MenuItemAttachmentUtilityTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void HasAncestorMenuContainer_DetectsInstallerAndChildrenSubMenu()
        {
            var installerParent = CreateGameObject("installer");
            installerParent.AddComponent<ModularAvatarMenuInstaller>();
            var installerChild = CreateChild(installerParent, "child");

            Assert.That(MenuItemAttachmentUtility.HasAncestorMenuContainer(installerChild), Is.True);

            var subMenuParent = CreateGameObject("submenu");
            var menuItem = subMenuParent.AddComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Type = PortableControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.Children;
            var subMenuChild = CreateChild(subMenuParent, "child");

            Assert.That(MenuItemAttachmentUtility.HasAncestorMenuContainer(subMenuChild), Is.True);
        }

        [Test]
        public void HasAncestorMenuContainer_IgnoresTargetAndNonChildrenSubMenu()
        {
            var parent = CreateGameObject("parent");
            var menuItem = parent.AddComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Type = PortableControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.MenuAsset;
            var target = CreateChild(parent, "target");
            target.AddComponent<ModularAvatarMenuInstaller>();

            Assert.That(MenuItemAttachmentUtility.HasAncestorMenuContainer(target), Is.False);
        }

        [Test]
        public void ResolveComponentTarget_AttachToggleConfiguresFixedSettings()
        {
            var target = CreateGameObject("target");

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.AttachToggle, target, null, null, null);

            Assert.That(result.ComponentTarget, Is.SameAs(target));
            Assert.That(result.CreatedGameObjectCount, Is.Zero);
            Assert.That(result.AddedMenuItemCount, Is.EqualTo(1));
            Assert.That(result.AddedMenuInstallerCount, Is.Zero);
            var menuItem = target.GetComponent<ModularAvatarMenuItem>();
            Assert.That(menuItem, Is.Not.Null);
            Assert.That(menuItem.PortableControl.Type, Is.EqualTo(PortableControlType.Toggle));
            Assert.That(menuItem.PortableControl.Value, Is.EqualTo(1f));
            Assert.That(menuItem.PortableControl.Parameter, Is.Empty);
            Assert.That(menuItem.isDefault, Is.False);
            Assert.That(menuItem.isSaved, Is.True);
            Assert.That(menuItem.automaticValue, Is.True);
        }

        [Test]
        public void ResolveComponentTarget_AttachTogglePreservesExistingToggleSettings()
        {
            var target = CreateGameObject("target");
            var menuItem = target.AddComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Type = PortableControlType.Toggle;
            menuItem.PortableControl.Parameter = "CustomParameter";
            menuItem.PortableControl.Value = 0.5f;
            menuItem.label = "Custom Label";
            menuItem.isDefault = true;
            menuItem.isSaved = false;
            menuItem.automaticValue = false;

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.AttachToggle, target, null, null, null);

            Assert.That(result.ComponentTarget, Is.SameAs(target));
            Assert.That(result.HasAdditions, Is.False);
            Assert.That(target.GetComponents<ModularAvatarMenuItem>(), Has.Length.EqualTo(1));
            Assert.That(menuItem.PortableControl.Type, Is.EqualTo(PortableControlType.Toggle));
            Assert.That(menuItem.PortableControl.Parameter, Is.EqualTo("CustomParameter"));
            Assert.That(menuItem.PortableControl.Value, Is.EqualTo(0.5f));
            Assert.That(menuItem.label, Is.EqualTo("Custom Label"));
            Assert.That(menuItem.isDefault, Is.True);
            Assert.That(menuItem.isSaved, Is.False);
            Assert.That(menuItem.automaticValue, Is.False);
        }

        [Test]
        public void ResolveComponentTarget_AttachToggleLeavesDifferentMenuItemTypeUntouched()
        {
            var target = CreateGameObject("target");
            var menuItem = target.AddComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Type = PortableControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.MenuAsset;
            menuItem.label = "Existing Sub Menu";

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.AttachToggle, target, null, null, null);

            Assert.That(result.ComponentTarget, Is.SameAs(target));
            Assert.That(result.HasAdditions, Is.False);
            Assert.That(target.GetComponents<ModularAvatarMenuItem>(), Has.Length.EqualTo(1));
            Assert.That(menuItem.PortableControl.Type, Is.EqualTo(PortableControlType.SubMenu));
            Assert.That(menuItem.MenuSource, Is.EqualTo(SubmenuSource.MenuAsset));
            Assert.That(menuItem.label, Is.EqualTo("Existing Sub Menu"));
        }

        [Test]
        public void ResolveComponentTarget_HierarchyUsesFixedParentAndReusesNamedChild()
        {
            var avatarRoot = CreateGameObject("avatar");

            var firstResult = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Setter",
                "Outfit");
            var menuObject = avatarRoot.transform.GetChild(0).gameObject;
            var subMenu = menuObject.GetComponent<ModularAvatarMenuItem>();
            Assert.That(subMenu.MenuSource, Is.EqualTo(SubmenuSource.Children));
            Assert.That(subMenu.isSaved, Is.True);
            subMenu.label = "Customized Parent";
            subMenu.isSaved = false;
            var toggle = firstResult.ComponentTarget.GetComponent<ModularAvatarMenuItem>();
            toggle.PortableControl.Parameter = "CustomParameter";
            toggle.label = "Customized Toggle";
            toggle.isDefault = true;
            toggle.isSaved = false;
            toggle.automaticValue = false;
            var secondResult = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Setter",
                "Outfit");

            Assert.That(secondResult.ComponentTarget, Is.SameAs(firstResult.ComponentTarget));
            Assert.That(firstResult.CreatedGameObjectCount, Is.EqualTo(2));
            Assert.That(firstResult.AddedMenuItemCount, Is.EqualTo(2));
            Assert.That(firstResult.AddedMenuInstallerCount, Is.EqualTo(1));
            Assert.That(secondResult.HasAdditions, Is.False);
            Assert.That(avatarRoot.transform.childCount, Is.EqualTo(1));
            Assert.That(menuObject.name, Is.EqualTo("Material Setter"));
            Assert.That(menuObject.transform.childCount, Is.EqualTo(1));
            Assert.That(firstResult.ComponentTarget.name, Is.EqualTo("Outfit"));
            Assert.That(menuObject.GetComponents<ModularAvatarMenuItem>(), Has.Length.EqualTo(1));
            Assert.That(menuObject.GetComponents<ModularAvatarMenuInstaller>(), Has.Length.EqualTo(1));
            Assert.That(firstResult.ComponentTarget.GetComponents<ModularAvatarMenuItem>(), Has.Length.EqualTo(1));

            Assert.That(subMenu.PortableControl.Type, Is.EqualTo(PortableControlType.SubMenu));
            Assert.That(subMenu.MenuSource, Is.EqualTo(SubmenuSource.Children));
            Assert.That(subMenu.label, Is.EqualTo("Customized Parent"));
            Assert.That(subMenu.isSaved, Is.False);
            Assert.That(toggle.PortableControl.Type, Is.EqualTo(PortableControlType.Toggle));
            Assert.That(toggle.PortableControl.Parameter, Is.EqualTo("CustomParameter"));
            Assert.That(toggle.label, Is.EqualTo("Customized Toggle"));
            Assert.That(toggle.isDefault, Is.True);
            Assert.That(toggle.isSaved, Is.False);
            Assert.That(toggle.automaticValue, Is.False);
        }

        [Test]
        public void ResolveComponentTarget_HierarchyAddsMissingInstallerWithoutChangingSubMenu()
        {
            var avatarRoot = CreateGameObject("avatar");
            var menuObject = CreateChild(avatarRoot, "Material Setter");
            var subMenu = menuObject.AddComponent<ModularAvatarMenuItem>();
            subMenu.PortableControl.Type = PortableControlType.SubMenu;
            subMenu.MenuSource = SubmenuSource.Children;
            subMenu.label = "Customized Parent";
            subMenu.isSaved = false;
            var toggleObject = CreateChild(menuObject, "Outfit");
            var toggle = toggleObject.AddComponent<ModularAvatarMenuItem>();
            toggle.PortableControl.Type = PortableControlType.Toggle;

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Setter",
                "Outfit");

            Assert.That(result.ComponentTarget, Is.SameAs(toggleObject));
            Assert.That(result.CreatedGameObjectCount, Is.Zero);
            Assert.That(result.AddedMenuItemCount, Is.Zero);
            Assert.That(result.AddedMenuInstallerCount, Is.EqualTo(1));
            Assert.That(menuObject.GetComponent<ModularAvatarMenuInstaller>(), Is.Not.Null);
            Assert.That(subMenu.MenuSource, Is.EqualTo(SubmenuSource.Children));
            Assert.That(subMenu.label, Is.EqualTo("Customized Parent"));
            Assert.That(subMenu.isSaved, Is.False);
        }

        [Test]
        public void ResolveComponentTarget_HierarchyCreatesNewParentForMenuAssetSubMenu()
        {
            var avatarRoot = CreateGameObject("avatar");
            var menuAssetParent = CreateChild(avatarRoot, "Material Setter");
            var menuAssetItem = menuAssetParent.AddComponent<ModularAvatarMenuItem>();
            menuAssetItem.PortableControl.Type = PortableControlType.SubMenu;
            menuAssetItem.MenuSource = SubmenuSource.MenuAsset;
            menuAssetItem.label = "Keep Menu Asset";
            menuAssetParent.AddComponent<ModularAvatarMenuInstaller>();

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Setter",
                "Outfit");

            Assert.That(avatarRoot.transform.childCount, Is.EqualTo(2));
            Assert.That(result.CreatedGameObjectCount, Is.EqualTo(2));
            Assert.That(result.AddedMenuItemCount, Is.EqualTo(2));
            Assert.That(result.AddedMenuInstallerCount, Is.EqualTo(1));
            Assert.That(menuAssetItem.MenuSource, Is.EqualTo(SubmenuSource.MenuAsset));
            Assert.That(menuAssetItem.label, Is.EqualTo("Keep Menu Asset"));
            var createdParent = result.ComponentTarget.transform.parent.gameObject;
            Assert.That(createdParent, Is.Not.SameAs(menuAssetParent));
            Assert.That(createdParent.name, Is.EqualTo("Material Setter"));
            var createdSubMenu = createdParent.GetComponent<ModularAvatarMenuItem>();
            Assert.That(createdSubMenu.PortableControl.Type, Is.EqualTo(PortableControlType.SubMenu));
            Assert.That(createdSubMenu.MenuSource, Is.EqualTo(SubmenuSource.Children));
        }

        [Test]
        public void ResolveComponentTarget_HierarchyCreatesNewObjectsForDifferentMenuItemTypes()
        {
            var avatarRoot = CreateGameObject("avatar");
            var wrongParent = CreateChild(avatarRoot, "Material Swap");
            var wrongParentItem = wrongParent.AddComponent<ModularAvatarMenuItem>();
            wrongParentItem.PortableControl.Type = PortableControlType.Toggle;
            wrongParentItem.label = "Keep Me";

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Swap",
                "Outfit");

            Assert.That(avatarRoot.transform.childCount, Is.EqualTo(2));
            Assert.That(result.CreatedGameObjectCount, Is.EqualTo(2));
            Assert.That(result.AddedMenuItemCount, Is.EqualTo(2));
            Assert.That(result.AddedMenuInstallerCount, Is.EqualTo(1));
            Assert.That(wrongParentItem.PortableControl.Type, Is.EqualTo(PortableControlType.Toggle));
            Assert.That(wrongParentItem.label, Is.EqualTo("Keep Me"));
            var createdParent = result.ComponentTarget.transform.parent.gameObject;
            Assert.That(createdParent.name, Is.EqualTo("Material Swap"));
            Assert.That(createdParent, Is.Not.SameAs(wrongParent));
            Assert.That(createdParent.GetComponent<ModularAvatarMenuItem>().PortableControl.Type,
                Is.EqualTo(PortableControlType.SubMenu));
            Assert.That(result.ComponentTarget.GetComponent<ModularAvatarMenuItem>().PortableControl.Type,
                Is.EqualTo(PortableControlType.Toggle));
        }

        [Test]
        public void ResolveComponentTarget_HierarchyCreatesNewToggleForDifferentChildType()
        {
            var avatarRoot = CreateGameObject("avatar");
            var menuObject = CreateChild(avatarRoot, "Material Setter");
            var subMenu = menuObject.AddComponent<ModularAvatarMenuItem>();
            subMenu.PortableControl.Type = PortableControlType.SubMenu;
            subMenu.MenuSource = SubmenuSource.Children;
            menuObject.AddComponent<ModularAvatarMenuInstaller>();
            var wrongToggle = CreateChild(menuObject, "Outfit");
            var wrongToggleItem = wrongToggle.AddComponent<ModularAvatarMenuItem>();
            wrongToggleItem.PortableControl.Type = PortableControlType.SubMenu;
            wrongToggleItem.label = "Keep Child";

            var result = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Setter",
                "Outfit");

            Assert.That(menuObject.transform.childCount, Is.EqualTo(2));
            Assert.That(result.CreatedGameObjectCount, Is.EqualTo(1));
            Assert.That(result.AddedMenuItemCount, Is.EqualTo(1));
            Assert.That(result.AddedMenuInstallerCount, Is.Zero);
            Assert.That(result.ComponentTarget.name, Is.EqualTo("Outfit"));
            Assert.That(result.ComponentTarget, Is.Not.SameAs(wrongToggle));
            Assert.That(result.ComponentTarget.GetComponent<ModularAvatarMenuItem>().PortableControl.Type,
                Is.EqualTo(PortableControlType.Toggle));
            Assert.That(wrongToggleItem.PortableControl.Type, Is.EqualTo(PortableControlType.SubMenu));
            Assert.That(wrongToggleItem.label, Is.EqualTo("Keep Child"));
        }

        [Test]
        public void ResolveComponentTarget_HierarchyReusesFixedParentAndCreatesDifferentToggles()
        {
            var avatarRoot = CreateGameObject("avatar");

            MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Swap",
                "First");
            var secondResult = MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Swap",
                "Second");

            Assert.That(avatarRoot.transform.childCount, Is.EqualTo(1));
            var menuObject = avatarRoot.transform.GetChild(0);
            Assert.That(menuObject.childCount, Is.EqualTo(2));
            Assert.That(menuObject.GetChild(0).name, Is.EqualTo("First"));
            Assert.That(menuObject.GetChild(1).name, Is.EqualTo("Second"));
            Assert.That(secondResult.CreatedGameObjectCount, Is.EqualTo(1));
            Assert.That(secondResult.AddedMenuItemCount, Is.EqualTo(1));
            Assert.That(secondResult.AddedMenuInstallerCount, Is.Zero);
        }

        [Test]
        public void ResolveComponentTarget_HierarchyCreationIsSingleUndoOperation()
        {
            var avatarRoot = CreateGameObject("avatar");
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create menu hierarchy test");

            MenuItemAttachmentUtility.ResolveComponentTarget(
                MenuItemAttachmentMode.CreateMenuHierarchy,
                null,
                avatarRoot,
                "Material Swap",
                "Toggle");
            Undo.CollapseUndoOperations(undoGroup);

            Assert.That(avatarRoot.transform.childCount, Is.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(avatarRoot.transform.childCount, Is.Zero);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateChild(GameObject parent, string name)
        {
            var child = CreateGameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
