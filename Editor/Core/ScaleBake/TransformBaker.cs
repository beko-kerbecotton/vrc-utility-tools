using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    public static class TransformBaker
    {
        public static void Bake(Transform avatarRoot, ScaleBakeSolveResult result, ScaleBakeSnapshot snapshot)
        {
            if (avatarRoot == null) throw new ArgumentNullException(nameof(avatarRoot));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (result.HasErrors) return;

            var transforms = new List<Transform>();
            var targetPositions = new Dictionary<Transform, Vector3>();
            CollectTargets(avatarRoot, result, snapshot, transforms, targetPositions);

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
            ScaleBakeSnapshot snapshot,
            List<Transform> transforms,
            Dictionary<Transform, Vector3> targetPositions)
        {
            transforms.Add(transform);

            var frameDeformation = result.GetFrameDeformation(transform);
            if (!ScaleBakeMatrixUtility.IsIdentity(frameDeformation))
            {
                targetPositions.Add(transform, frameDeformation.MultiplyPoint3x4(snapshot.GetPosition(transform)));
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                CollectTargets(transform.GetChild(index), result, snapshot, transforms, targetPositions);
            }
        }
    }
}
