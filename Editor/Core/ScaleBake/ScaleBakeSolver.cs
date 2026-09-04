using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    public static class ScaleBakeSolver
    {
        private const float SingularScaleThreshold = 1e-6f;
        private const float UniformScaleRelativeTolerance = 1e-4f;

        public static ScaleBakeSolveResult Solve(Transform avatarRoot)
        {
            if (avatarRoot == null) throw new ArgumentNullException(nameof(avatarRoot));

            var frameDeformations = new Dictionary<Transform, Matrix4x4>();
            var contentDeformations = new Dictionary<Transform, Matrix4x4>();
            var issues = new List<ScaleBakeIssue>();
            var hierarchyInspectionTargets = CollectHierarchyInspectionTargets(avatarRoot);

            Visit(
                avatarRoot,
                Matrix4x4.identity,
                false,
                frameDeformations,
                contentDeformations,
                issues);
            InspectHierarchy(avatarRoot, hierarchyInspectionTargets, issues);

            return new ScaleBakeSolveResult(frameDeformations, contentDeformations, issues);
        }

        private static void Visit(
            Transform transform,
            Matrix4x4 frameDeformation,
            bool affectedByAncestor,
            Dictionary<Transform, Matrix4x4> frameDeformations,
            Dictionary<Transform, Matrix4x4> contentDeformations,
            List<ScaleBakeIssue> issues)
        {
            var component = transform.GetComponent<NonUniformScaleBake>();
            var contentDeformation = frameDeformation;
            var hasComponent = component != null;

            if (hasComponent && IsValidComponentScale(component.Scale, transform, issues))
            {
                // Δ_X = M_X * Scale(S) * M_X⁻¹ は、Xのローカル座標系でXの原点を中心に
                // S倍する変形をワールド空間で表したものになる。
                // 外側の累積変形をDとすると、ネストしたXの追加変形は
                // (D*M_X)*Scale(S)*(D*M_X)⁻¹ だが、累積結果は
                // D' = D*M_X*Scale(S)*M_X⁻¹ = D*Δ_X に簡約できる。
                // したがってTransformを逐次書き換えず、元の行列をルートから順に合成すればよい。
                // M_X⁻¹には、Unityが保持する精度の良いworldToLocalMatrixを使用する。
                var delta = transform.localToWorldMatrix
                            * Matrix4x4.Scale(component.Scale)
                            * transform.worldToLocalMatrix;
                contentDeformation = frameDeformation * delta;
            }

            var isAffected = affectedByAncestor || hasComponent;
            if (isAffected)
            {
                frameDeformations.Add(transform, frameDeformation);
                contentDeformations.Add(transform, contentDeformation);
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                Visit(
                    transform.GetChild(index),
                    contentDeformation,
                    isAffected,
                    frameDeformations,
                    contentDeformations,
                    issues);
            }
        }

        private static bool IsValidComponentScale(
            Vector3 scale,
            Transform target,
            List<ScaleBakeIssue> issues)
        {
            if (!IsFinite(scale.x) || !IsFinite(scale.y) || !IsFinite(scale.z))
            {
                issues.Add(new ScaleBakeIssue(ScaleBakeIssueSeverity.Error, ScaleBakeIssueCode.InvalidScale, target));
                return false;
            }

            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                issues.Add(new ScaleBakeIssue(ScaleBakeIssueSeverity.Error, ScaleBakeIssueCode.NonPositiveScale, target));
                return false;
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static HashSet<Transform> CollectHierarchyInspectionTargets(Transform avatarRoot)
        {
            var targets = new HashSet<Transform>();
            CollectBakeRanges(avatarRoot, avatarRoot, targets);
            return targets;
        }

        private static void CollectBakeRanges(Transform transform, Transform avatarRoot, HashSet<Transform> targets)
        {
            if (transform.GetComponent<NonUniformScaleBake>() != null)
            {
                var ancestor = transform;
                while (ancestor != null)
                {
                    targets.Add(ancestor);
                    if (ancestor == avatarRoot) break;
                    ancestor = ancestor.parent;
                }

                AddDescendants(transform, targets);
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                CollectBakeRanges(transform.GetChild(index), avatarRoot, targets);
            }
        }

        private static void AddDescendants(Transform transform, HashSet<Transform> targets)
        {
            targets.Add(transform);
            for (var index = 0; index < transform.childCount; index++)
            {
                AddDescendants(transform.GetChild(index), targets);
            }
        }

        private static void InspectHierarchy(
            Transform transform,
            HashSet<Transform> inspectionTargets,
            List<ScaleBakeIssue> issues)
        {
            if (inspectionTargets.Contains(transform))
            {
                var scale = transform.localScale;
                if (Mathf.Abs(scale.x) <= SingularScaleThreshold
                    || Mathf.Abs(scale.y) <= SingularScaleThreshold
                    || Mathf.Abs(scale.z) <= SingularScaleThreshold)
                {
                    issues.Add(new ScaleBakeIssue(
                        ScaleBakeIssueSeverity.Error,
                        ScaleBakeIssueCode.HierarchySingularScale,
                        transform));
                }
                else
                {
                    if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
                    {
                        issues.Add(new ScaleBakeIssue(
                            ScaleBakeIssueSeverity.Warning,
                            ScaleBakeIssueCode.HierarchyNegativeScale,
                            transform));
                    }

                    if (!IsUniform(scale))
                    {
                        issues.Add(new ScaleBakeIssue(
                            ScaleBakeIssueSeverity.Warning,
                            ScaleBakeIssueCode.HierarchyNonUniformScale,
                            transform));
                    }
                }
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                InspectHierarchy(transform.GetChild(index), inspectionTargets, issues);
            }
        }

        private static bool IsUniform(Vector3 scale)
        {
            var maximumMagnitude = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var range = Mathf.Max(scale.x, scale.y, scale.z) - Mathf.Min(scale.x, scale.y, scale.z);
            return range <= UniformScaleRelativeTolerance * maximumMagnitude;
        }
    }
}
