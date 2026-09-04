using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    public enum ScaleBakeIssueSeverity
    {
        Warning,
        Error
    }

    public enum ScaleBakeIssueCode
    {
        NonPositiveScale,
        InvalidScale,
        HierarchySingularScale,
        HierarchyNegativeScale,
        HierarchyNonUniformScale
    }

    public readonly struct ScaleBakeIssue
    {
        public ScaleBakeIssueSeverity Severity { get; }
        public ScaleBakeIssueCode Code { get; }
        public Transform Target { get; }

        public ScaleBakeIssue(ScaleBakeIssueSeverity severity, ScaleBakeIssueCode code, Transform target)
        {
            Severity = severity;
            Code = code;
            Target = target;
        }
    }
}
