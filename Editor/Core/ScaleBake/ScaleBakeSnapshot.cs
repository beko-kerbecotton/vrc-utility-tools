using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    public sealed class ScaleBakeSnapshot
    {
        private readonly Dictionary<Transform, Matrix4x4> _localToWorldMatrices;
        private readonly Dictionary<Transform, Vector3> _positions;

        private ScaleBakeSnapshot(
            Dictionary<Transform, Matrix4x4> localToWorldMatrices,
            Dictionary<Transform, Vector3> positions)
        {
            _localToWorldMatrices = localToWorldMatrices;
            _positions = positions;
        }

        public static ScaleBakeSnapshot Capture(Transform avatarRoot)
        {
            if (avatarRoot == null) throw new ArgumentNullException(nameof(avatarRoot));

            var matrices = new Dictionary<Transform, Matrix4x4>();
            var positions = new Dictionary<Transform, Vector3>();
            var transforms = avatarRoot.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                matrices.Add(transforms[index], transforms[index].localToWorldMatrix);
                positions.Add(transforms[index], transforms[index].position);
            }

            return new ScaleBakeSnapshot(matrices, positions);
        }

        public bool Contains(Transform transform)
        {
            return transform != null && _localToWorldMatrices.ContainsKey(transform);
        }

        public Matrix4x4 GetLocalToWorld(Transform transform)
        {
            if (transform == null) return Matrix4x4.identity;
            if (_localToWorldMatrices.TryGetValue(transform, out var matrix)) return matrix;

            // アバター外のボーンはBakeで動かないため、現在値がBake前の値と一致する。
            return transform.localToWorldMatrix;
        }

        public Vector3 GetPosition(Transform transform)
        {
            if (transform == null) return Vector3.zero;
            if (_positions.TryGetValue(transform, out var position)) return position;

            // アバター外のTransformはBakeで動かないため、現在値がBake前の値と一致する。
            return transform.position;
        }
    }
}
