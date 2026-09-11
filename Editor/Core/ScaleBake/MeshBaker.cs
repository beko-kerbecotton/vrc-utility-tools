using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    /// <summary>TransformBaker.Bakeの後に呼ばれることを前提に、変形後のTransformを基準としてMeshを補正する。</summary>
    public static class MeshBaker
    {
        public static void Bake(Transform avatarRoot, ScaleBakeSolveResult result, ScaleBakeSnapshot snapshot)
        {
            if (avatarRoot == null)
            {
                throw new ArgumentNullException(nameof(avatarRoot));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (result.HasErrors)
            {
                return;
            }

            var cache = new BakedMeshCache();
            var skinnedRenderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var index = 0; index < skinnedRenderers.Length; index++)
            {
                BakeSkinned(skinnedRenderers[index], result, snapshot, cache);
            }

            var meshRenderers = avatarRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < meshRenderers.Length; index++)
            {
                var filter = meshRenderers[index].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }
                filter.sharedMesh = BakeVertices(filter.sharedMesh, meshRenderers[index].transform, result, snapshot, cache);
            }
        }

        private static void BakeSkinned(
            SkinnedMeshRenderer renderer,
            ScaleBakeSolveResult result,
            ScaleBakeSnapshot snapshot,
            BakedMeshCache cache)
        {
            var source = renderer.sharedMesh;
            if (source == null)
            {
                return;
            }
            var bones = renderer.bones;
            var bindposes = source.bindposes;
            if (bones == null || bones.Length == 0 || bindposes.Length == 0)
            {
                var baked = BakeVertices(source, renderer.transform, result, snapshot, cache);
                if (baked != source)
                {
                    renderer.sharedMesh = baked;
                    renderer.localBounds = baked.bounds;
                }
                return;
            }

            var newBindposes = (Matrix4x4[])bindposes.Clone();
            var changed = false;
            var count = Mathf.Min(bones.Length, bindposes.Length);
            for (var index = 0; index < count; index++)
            {
                var bone = bones[index];
                if (bone == null)
                {
                    continue;
                }
                var deformation = result.GetContentDeformation(bone);
                if (ScaleBakeMatrixUtility.IsIdentity(deformation))
                {
                    continue;
                }
                newBindposes[index] = bone.worldToLocalMatrix * deformation
                    * snapshot.GetLocalToWorld(bone) * bindposes[index];
                changed = true;
            }

            if (!changed)
            {
                return;
            }
            var originalBounds = renderer.localBounds;
            if (!cache.TryGetBindposeBaked(source, newBindposes, out var bakedMesh))
            {
                bakedMesh = UnityEngine.Object.Instantiate(source);
                bakedMesh.name = source.name;
                bakedMesh.bindposes = newBindposes;
                cache.AddBindposeBaked(source, newBindposes, bakedMesh);
            }
            renderer.sharedMesh = bakedMesh;
            UpdateSkinnedBounds(renderer, bones, originalBounds, result, snapshot);
        }

        private static Mesh BakeVertices(
            Mesh source,
            Transform transform,
            ScaleBakeSolveResult result,
            ScaleBakeSnapshot snapshot,
            BakedMeshCache cache)
        {
            var transformation = transform.worldToLocalMatrix * result.GetContentDeformation(transform)
                * snapshot.GetLocalToWorld(transform);
            if (ScaleBakeMatrixUtility.IsIdentity(transformation))
            {
                return source;
            }
            if (cache.TryGetVertexBaked(source, transformation, out var cached))
            {
                return cached;
            }

            var baked = UnityEngine.Object.Instantiate(source);
            baked.name = source.name;
            var normalTransformation = ScaleBakeMatrixUtility.InverseTranspose(transformation);

            var vertices = baked.vertices;
            if (vertices.Length == 0)
            {
                // 読み出せない頂点を空配列で上書きするとMeshを壊すため、複製を破棄して元Meshを維持する。
                UnityEngine.Object.DestroyImmediate(baked);
                return source;
            }
            for (var index = 0; index < vertices.Length; index++)
            {
                vertices[index] = transformation.MultiplyPoint3x4(vertices[index]);
            }
            baked.vertices = vertices;

            var normals = baked.normals;
            for (var index = 0; index < normals.Length; index++)
            {
                normals[index] = normalTransformation.MultiplyVector(normals[index]).normalized;
            }
            if (normals.Length > 0)
            {
                baked.normals = normals;
            }

            var tangents = baked.tangents;
            for (var index = 0; index < tangents.Length; index++)
            {
                var tangent = tangents[index];
                var direction = transformation.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
                tangents[index] = new Vector4(direction.x, direction.y, direction.z, tangent.w);
            }
            if (tangents.Length > 0)
            {
                baked.tangents = tangents;
            }

            BakeBlendShapes(baked, transformation, normalTransformation);
            baked.RecalculateBounds();
            cache.AddVertexBaked(source, transformation, baked);
            return baked;
        }

        private static void BakeBlendShapes(Mesh mesh, Matrix4x4 transformation, Matrix4x4 normalTransformation)
        {
            var frames = new List<BlendShapeFrame>();
            for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
            {
                var name = mesh.GetBlendShapeName(shapeIndex);
                for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++)
                {
                    var deltaVertices = new Vector3[mesh.vertexCount];
                    var deltaNormals = new Vector3[mesh.vertexCount];
                    var deltaTangents = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    for (var index = 0; index < mesh.vertexCount; index++)
                    {
                        deltaVertices[index] = transformation.MultiplyVector(deltaVertices[index]);
                        // 法線差分は単位法線間の差なので厳密には非線形だが、ここでは逆転置による線形近似を使う。
                        deltaNormals[index] = normalTransformation.MultiplyVector(deltaNormals[index]);
                        deltaTangents[index] = transformation.MultiplyVector(deltaTangents[index]);
                    }
                    frames.Add(new BlendShapeFrame(name, mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex),
                        deltaVertices, deltaNormals, deltaTangents));
                }
            }

            if (frames.Count == 0)
            {
                return;
            }
            mesh.ClearBlendShapes();
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                mesh.AddBlendShapeFrame(frame.Name, frame.Weight,
                    frame.DeltaVertices, frame.DeltaNormals, frame.DeltaTangents);
            }
        }

        private static void UpdateSkinnedBounds(
            SkinnedMeshRenderer renderer,
            Transform[] bones,
            Bounds originalBounds,
            ScaleBakeSolveResult result,
            ScaleBakeSnapshot snapshot)
        {
            var anchor = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
            var originalCorners = GetCorners(originalBounds, snapshot.GetLocalToWorld(anchor));
            var deformations = new List<Matrix4x4> { Matrix4x4.identity };
            for (var boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            {
                if (bones[boneIndex] == null)
                {
                    continue;
                }
                var deformation = result.GetContentDeformation(bones[boneIndex]);
                if (!ContainsMatrix(deformations, deformation))
                {
                    deformations.Add(deformation);
                }
            }

            // 標準bindposeでは各ボーンの変形前位置は同じqであり、スキニング後はC_i qの凸結合になる。
            // よって各C_iで変換した8隅の凸包を囲むAABBは、変形後の全頂点を保守的に含む。
            var initialized = false;
            var min = Vector3.zero;
            var max = Vector3.zero;
            for (var deformationIndex = 0; deformationIndex < deformations.Count; deformationIndex++)
            {
                for (var cornerIndex = 0; cornerIndex < originalCorners.Length; cornerIndex++)
                {
                    var world = deformations[deformationIndex].MultiplyPoint3x4(originalCorners[cornerIndex]);
                    var local = anchor.worldToLocalMatrix.MultiplyPoint3x4(world);
                    if (!initialized)
                    {
                        min = local;
                        max = local;
                        initialized = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
            }
            renderer.localBounds = new Bounds((min + max) * 0.5f, max - min);
        }

        private static Vector3[] GetCorners(Bounds bounds, Matrix4x4 localToWorld)
        {
            var result = new Vector3[8];
            var index = 0;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        result[index++] = localToWorld.MultiplyPoint3x4(bounds.center
                            + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                    }
                }
            }
            return result;
        }

        private static bool ContainsMatrix(List<Matrix4x4> matrices, Matrix4x4 candidate)
        {
            for (var index = 0; index < matrices.Count; index++)
            {
                var equal = true;
                for (var row = 0; row < 4 && equal; row++)
                {
                    for (var column = 0; column < 4; column++)
                    {
                        if (matrices[index][row, column] != candidate[row, column])
                        {
                            equal = false;
                            break;
                        }
                    }
                }
                if (equal)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class BlendShapeFrame
        {
            internal readonly string Name;
            internal readonly float Weight;
            internal readonly Vector3[] DeltaVertices;
            internal readonly Vector3[] DeltaNormals;
            internal readonly Vector3[] DeltaTangents;
            internal BlendShapeFrame(string name, float weight, Vector3[] vertices, Vector3[] normals, Vector3[] tangents)
            {
                Name = name;
                Weight = weight;
                DeltaVertices = vertices;
                DeltaNormals = normals;
                DeltaTangents = tangents;
            }
        }
    }
}
