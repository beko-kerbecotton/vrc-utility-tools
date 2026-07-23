using net.bekobeko.utilitytools.core;

namespace net.bekobeko.utilitytools.windows
{
    public static class ConflictSessionFactory
    {
        public static ConflictResolutionSession CreateIfMerge(ExistingComponentHandlingMode mode)
        {
            return mode == ExistingComponentHandlingMode.Merge
                ? new ConflictResolutionSession(ConflictResolutionDialog.Show)
                : null;
        }
    }
}
