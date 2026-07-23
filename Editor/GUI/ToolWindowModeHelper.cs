using net.bekobeko.utilitytools.core;

namespace net.bekobeko.utilitytools.windows
{
    public static class ToolWindowModeHelper
    {
        public static bool ShouldRevertAttachment(ComponentBuildStatus status)
        {
            return status == ComponentBuildStatus.Cancelled ||
                   status == ComponentBuildStatus.InvalidTarget ||
                   status == ComponentBuildStatus.NoAssignments;
        }

        public static MenuItemAttachmentMode GetEffectiveAttachmentMode(
            ToolUIMode uiMode,
            MenuItemAttachmentMode advancedMode)
        {
            return uiMode == ToolUIMode.Normal
                ? MenuItemAttachmentMode.CreateMenuHierarchy
                : advancedMode;
        }

        public static ExistingComponentHandlingMode GetEffectiveHandlingMode(
            ToolUIMode uiMode,
            ExistingComponentHandlingMode advancedMode)
        {
            return uiMode == ToolUIMode.Normal
                ? ExistingComponentHandlingMode.Skip
                : advancedMode;
        }
    }
}
