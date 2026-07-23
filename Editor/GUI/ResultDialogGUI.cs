using System.Collections.Generic;
using net.bekobeko.utilitytools.core;
using net.bekobeko.utilitytools.localization;
using UnityEditor;

namespace net.bekobeko.utilitytools.windows
{
    public static class ResultDialogGUI
    {
        public static string GetBuildStatusMessageKey(string prefix, ComponentBuildStatus status)
        {
            var statusName = status.ToString();
            return $"{prefix}.{char.ToLowerInvariant(statusName[0])}{statusName.Substring(1)}";
        }

        public static void ShowBuildResult(
            string titleKey,
            string messageKeyPrefix,
            ComponentBuildStatus status)
        {
            Show(titleKey, GetBuildStatusMessageKey(messageKeyPrefix, status));
        }

        public static void ShowBuildResult(
            string titleKey,
            string messageKeyPrefix,
            ComponentBuildStatus status,
            MenuItemAttachmentResult attachmentResult)
        {
            if (attachmentResult == null || !attachmentResult.HasAdditions)
            {
                ShowBuildResult(titleKey, messageKeyPrefix, status);
                return;
            }

            EditorUtility.DisplayDialog(
                EditorLocalization.Get(titleKey),
                GetBuildResultMessage(messageKeyPrefix, status, attachmentResult),
                EditorLocalization.Get("common.ok"));
        }

        public static string GetBuildResultMessage(
            string messageKeyPrefix,
            ComponentBuildStatus status,
            MenuItemAttachmentResult attachmentResult)
        {
            if (attachmentResult == null || !attachmentResult.HasAdditions)
                return EditorLocalization.Get(GetBuildStatusMessageKey(messageKeyPrefix, status));

            var componentMessage = status == ComponentBuildStatus.NoChanges ||
                                   status == ComponentBuildStatus.AlreadyExists
                ? EditorLocalization.Get("menuAttach.componentUnchanged")
                : EditorLocalization.Get(GetBuildStatusMessageKey(messageKeyPrefix, status));

            var additions = new List<string>();
            if (attachmentResult.CreatedGameObjectCount > 0)
                additions.Add(EditorLocalization.Format(
                    "menuAttach.createdGameObjects", attachmentResult.CreatedGameObjectCount));
            if (attachmentResult.AddedMenuItemCount > 0)
                additions.Add(EditorLocalization.Format(
                    "menuAttach.addedMenuItems", attachmentResult.AddedMenuItemCount));
            if (attachmentResult.AddedMenuInstallerCount > 0)
                additions.Add(EditorLocalization.Format(
                    "menuAttach.addedMenuInstallers", attachmentResult.AddedMenuInstallerCount));

            return $"{componentMessage}\n\n{EditorLocalization.Get("menuAttach.resultHeader")}\n" +
                   string.Join("\n", additions);
        }

        public static void Show(string titleKey, string messageKey, params object[] messageArguments)
        {
            EditorUtility.DisplayDialog(
                EditorLocalization.Get(titleKey),
                EditorLocalization.Format(messageKey, messageArguments),
                EditorLocalization.Get("common.ok"));
        }
    }
}
