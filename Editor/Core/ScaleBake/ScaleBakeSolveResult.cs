using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    public sealed class ScaleBakeSolveResult
    {
        private readonly Dictionary<Transform, Matrix4x4> _frameDeformations;
        private readonly Dictionary<Transform, Matrix4x4> _contentDeformations;

        public IReadOnlyList<ScaleBakeIssue> Issues { get; }
        public bool HasErrors { get; }
        public IReadOnlyCollection<Transform> AffectedTransforms { get; }

        internal ScaleBakeSolveResult(
            Dictionary<Transform, Matrix4x4> frameDeformations,
            Dictionary<Transform, Matrix4x4> contentDeformations,
            List<ScaleBakeIssue> issues)
        {
            _frameDeformations = new Dictionary<Transform, Matrix4x4>(frameDeformations);
            _contentDeformations = new Dictionary<Transform, Matrix4x4>(contentDeformations);
            Issues = new ReadOnlyCollection<ScaleBakeIssue>(new List<ScaleBakeIssue>(issues));
            AffectedTransforms = new ReadOnlyCollection<Transform>(new List<Transform>(_frameDeformations.Keys));

            for (var index = 0; index < issues.Count; index++)
            {
                if (issues[index].Severity == ScaleBakeIssueSeverity.Error)
                {
                    HasErrors = true;
                    break;
                }
            }
        }

        public bool IsAffected(Transform transform)
        {
            return transform != null && _frameDeformations.ContainsKey(transform);
        }

        public Matrix4x4 GetFrameDeformation(Transform transform)
        {
            return transform != null && _frameDeformations.TryGetValue(transform, out var deformation)
                ? deformation
                : Matrix4x4.identity;
        }

        public Matrix4x4 GetContentDeformation(Transform transform)
        {
            return transform != null && _contentDeformations.TryGetValue(transform, out var deformation)
                ? deformation
                : Matrix4x4.identity;
        }
    }
}
