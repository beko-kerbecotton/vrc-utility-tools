using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using net.bekobeko.utilitytools.windows;
using NUnit.Framework;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class ResultDialogGUITests
    {
        [TestCase("materialSetter", ComponentBuildStatus.Success, "materialSetter.success")]
        [TestCase("materialSetter", ComponentBuildStatus.Updated, "materialSetter.updated")]
        [TestCase("materialSetter", ComponentBuildStatus.NoChanges, "materialSetter.noChanges")]
        [TestCase("materialSetter", ComponentBuildStatus.AlreadyExists, "materialSetter.alreadyExists")]
        [TestCase("materialSetter", ComponentBuildStatus.Cancelled, "materialSetter.cancelled")]
        [TestCase("materialSetter", ComponentBuildStatus.NoAssignments, "materialSetter.noAssignments")]
        [TestCase("materialSetter", ComponentBuildStatus.InvalidTarget, "materialSetter.invalidTarget")]
        [TestCase("materialSwap", ComponentBuildStatus.Success, "materialSwap.success")]
        [TestCase("materialSwap", ComponentBuildStatus.Updated, "materialSwap.updated")]
        [TestCase("materialSwap", ComponentBuildStatus.NoChanges, "materialSwap.noChanges")]
        [TestCase("materialSwap", ComponentBuildStatus.AlreadyExists, "materialSwap.alreadyExists")]
        [TestCase("materialSwap", ComponentBuildStatus.Cancelled, "materialSwap.cancelled")]
        [TestCase("materialSwap", ComponentBuildStatus.NoAssignments, "materialSwap.noAssignments")]
        [TestCase("materialSwap", ComponentBuildStatus.InvalidTarget, "materialSwap.invalidTarget")]
        public void GetBuildStatusMessageKey_ReturnsExistingLocalizationKey(
            string prefix,
            ComponentBuildStatus status,
            string expected)
        {
            Assert.That(
                ResultDialogGUI.GetBuildStatusMessageKey(prefix, status),
                Is.EqualTo(expected));
        }

        [Test]
        public void GetBuildResultMessage_NoChangesWithMenuAdditionReportsAddition()
        {
            var target = new UnityEngine.GameObject("target");
            var originalLanguage = EditorLocalization.Language;
            try
            {
                EditorLocalization.Language = ToolLanguage.English;
                var attachmentResult = MenuItemAttachmentUtility.ResolveComponentTarget(
                    MenuItemAttachmentMode.AttachToggle,
                    target,
                    null,
                    null,
                    null);

                var message = ResultDialogGUI.GetBuildResultMessage(
                    "materialSetter",
                    ComponentBuildStatus.NoChanges,
                    attachmentResult);

                Assert.That(message, Does.Contain("Material component"));
                Assert.That(message, Does.Contain("Added MA Menu Items: 1"));
                Assert.That(message, Does.Not.Contain("No settings were changed"));
            }
            finally
            {
                EditorLocalization.Language = originalLanguage;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
