using System;

namespace net.bekobeko.utilitytools.core
{
    public enum ExistingComponentHandlingMode
    {
        Skip,
        Merge,
        Overwrite
    }

    public enum ConflictResolutionAction
    {
        Skip,
        Overwrite,
        Cancel
    }

    public sealed class ConflictInfo
    {
        public string Subject { get; }
        public string ExistingValue { get; }
        public string GeneratedValue { get; }

        public ConflictInfo(string subject, string existingValue, string generatedValue)
        {
            Subject = subject ?? string.Empty;
            ExistingValue = existingValue ?? string.Empty;
            GeneratedValue = generatedValue ?? string.Empty;
        }
    }

    public readonly struct ConflictResolutionDecision
    {
        public ConflictResolutionAction Action { get; }
        public bool ApplyToAll { get; }

        public ConflictResolutionDecision(ConflictResolutionAction action, bool applyToAll)
        {
            Action = action;
            ApplyToAll = applyToAll && action != ConflictResolutionAction.Cancel;
        }
    }

    public sealed class ConflictResolutionSession
    {
        private readonly Func<ConflictInfo, ConflictResolutionDecision> _resolver;
        private ConflictResolutionAction? _applyToAllAction;

        public ConflictResolutionSession(Func<ConflictInfo, ConflictResolutionDecision> resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public ConflictResolutionAction Resolve(ConflictInfo conflict)
        {
            if (_applyToAllAction.HasValue) return _applyToAllAction.Value;

            var decision = _resolver(conflict ?? new ConflictInfo(string.Empty, string.Empty, string.Empty));
            if (decision.ApplyToAll) _applyToAllAction = decision.Action;
            return decision.Action;
        }
    }
}
