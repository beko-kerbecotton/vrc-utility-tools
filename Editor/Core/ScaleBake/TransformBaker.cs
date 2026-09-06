using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    public static class TransformBaker
    {
        public static void Bake(Transform avatarRoot, ScaleBakeSolveResult result)
        {
            if (avatarRoot == null) throw new ArgumentNullException(nameof(avatarRoot));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.HasErrors) return;

            var transforms = new List<Transform>();
            var targetPositions = new Dictionary<Transform, Vector3>();
            CollectTargets(avatarRoot, result, transforms, targetPositions);

            for (var index = 0; index < transforms.Count; index++)
            {
                var transform = transforms[index];
                if (targetPositions.TryGetValue(transform, out var targetPosition))
                {
                    transform.position = targetPosition;
                }
            }
        }

        private static void CollectTargets(
            Transform transform,
            ScaleBakeSolveResult result,
            List<Transform> transforms,
            Dictionary<Transform, Vector3> targetPositions)
        {
            transforms.Add(transform);

            var frameDeformation = result.GetFrameDeformation(transform);
            if (!IsIdentity(frameDeformation))
            {
                targetPositions.Add(transform, frameDeformation.MultiplyPoint3x4(transform.position));
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                CollectTargets(transform.GetChild(index), result, transforms, targetPositions);
            }
        }

        private static bool IsIdentity(Matrix4x4 matrix)
        {
            var identity = Matrix4x4.identity;
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    if (matrix[row, column] != identity[row, column]) return false;
                }
            }

            return true;
        }
    }
}
