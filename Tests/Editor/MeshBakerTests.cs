using System.Collections.Generic;
using net.bekobeko.utilitytools.core.scalebake;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class MeshBakerTests
    {
        private const float Tolerance = 1e-4f;
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }
            _objects.Clear();
        }

        [Test]
        public void Bake_SingleBone_MatchesOrdinaryScale()
        {
            AssertSkinnedOracle(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, false);
        }

        [Test]
        public void Bake_TwoBones_MatchesOrdinaryScale()
        {
            AssertSkinnedOracle(new BoneWeight { boneIndex0 = 0, weight0 = 0.35f, boneIndex1 = 1, weight1 = 0.65f }, false);
        }

        [Test]
        public void Bake_InsideAndOutsideBones_MatchesOrdinaryScale()
        {
            AssertSkinnedOracle(new BoneWeight { boneIndex0 = 0, weight0 = 0.4f, boneIndex1 = 1, weight1 = 0.6f }, true);
        }

        [Test]
        public void Bake_RotatedBone_DoesNotShearRestShape()
        {
            var root = CreateObject("Root").transform;
            var bone = CreateChild(root, Vector3.zero);
            AddBake(bone, new Vector3(2f, 1f, 0.5f));
            var mesh = CreateMesh(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[]
            {
                FullWeight(0), FullWeight(0), FullWeight(0)
            }, new[] { bone.worldToLocalMatrix });
            var renderer = CreateSkinned(root, mesh, new[] { bone });
            BakeAll(root);
            var rest = SkinVertices(renderer);
            var originalDistance = Vector3.Distance(mesh.vertices[0], mesh.vertices[1]);
            var restDistance = Vector3.Distance(rest[0], rest[1]);

            Assert.That(restDistance, Is.Not.EqualTo(originalDistance).Within(Tolerance));
            Assert.That(restDistance, Is.EqualTo(2f).Within(Tolerance));

            bone.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var rotated = SkinVertices(renderer);

            Assert.That(Vector3.Distance(rotated[0], rotated[1]), Is.EqualTo(Vector3.Distance(rest[0], rest[1])).Within(Tolerance));
            Assert.That(Vector3.Distance(rotated[0], rotated[2]), Is.EqualTo(Vector3.Distance(rest[0], rest[2])).Within(Tolerance));
            Assert.That(Vector3.Distance(rotated[1], rotated[2]), Is.EqualTo(Vector3.Distance(rest[1], rest[2])).Within(Tolerance));
        }

        [Test]
        public void Bake_BonelessBlendShape_TransformsDelta()
        {
            var root = CreateObject("Root").transform;
            var renderer = CreateObject("Renderer").AddComponent<SkinnedMeshRenderer>();
            renderer.transform.SetParent(root, false);
            AddBake(renderer.transform, new Vector3(2f, 3f, 4f));
            var mesh = CreateMesh(new[] { Vector3.zero }, new[] { default(BoneWeight) }, new Matrix4x4[0]);
            mesh.AddBlendShapeFrame("Shape", 100f, new[] { Vector3.one }, new[] { Vector3.up }, new[] { Vector3.right });
            renderer.sharedMesh = mesh;
            BakeAll(root);

            var vertices = new Vector3[1];
            var normals = new Vector3[1];
            var tangents = new Vector3[1];
            renderer.sharedMesh.GetBlendShapeFrameVertices(0, 0, vertices, normals, tangents);
            AssertVector(vertices[0], new Vector3(2f, 3f, 4f));
        }

        [Test]
        public void Bake_VertexNormal_RemainsPerpendicularToFace()
        {
            var root = CreateObject("Root").transform;
            var target = CreateChild(root, Vector3.zero, Quaternion.Euler(10f, 20f, 30f));
            AddBake(target, new Vector3(2f, 3f, 0.5f));
            var mesh = CreateMesh(new[] { Vector3.zero, Vector3.right, Vector3.up },
                new BoneWeight[3], new Matrix4x4[0]);
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.triangles = new[] { 0, 1, 2 };
            var filter = target.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            target.gameObject.AddComponent<MeshRenderer>();
            BakeAll(root);

            var vertices = filter.sharedMesh.vertices;
            var normal = filter.sharedMesh.normals[0];
            Assert.That(Vector3.Dot(vertices[1] - vertices[0], normal), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Vector3.Dot(vertices[2] - vertices[0], normal), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Bake_SharedMeshAndBones_ReusesBakedMesh()
        {
            var root = CreateObject("Root").transform;
            var bone = CreateChild(root, Vector3.zero);
            AddBake(bone, new Vector3(2f, 3f, 4f));
            var mesh = CreateMesh(new[] { Vector3.one }, new[] { FullWeight(0) }, new[] { bone.worldToLocalMatrix });
            var first = CreateSkinned(root, mesh, new[] { bone });
            var second = CreateSkinned(root, mesh, new[] { bone });
            BakeAll(root);
            Assert.That(first.sharedMesh, Is.SameAs(second.sharedMesh));
        }

        [Test]
        public void Bake_DoesNotModifySourceMesh()
        {
            var root = CreateObject("Root").transform;
            var bone = CreateChild(root, Vector3.zero);
            AddBake(bone, new Vector3(2f, 3f, 4f));
            var mesh = CreateMesh(new[] { Vector3.one }, new[] { FullWeight(0) }, new[] { bone.worldToLocalMatrix });
            var vertices = mesh.vertices;
            var bindposes = mesh.bindposes;
            CreateSkinned(root, mesh, new[] { bone });
            BakeAll(root);
            AssertVector(mesh.vertices[0], vertices[0]);
            AssertMatrix(mesh.bindposes[0], bindposes[0]);
        }

        [Test]
        public void Bake_LocalBounds_ContainsAllSkinnedVertices()
        {
            var root = CreateObject("Root").transform;
            var firstBone = CreateChild(root, new Vector3(-1f, 0f, 0f));
            var secondBone = CreateChild(root, new Vector3(1f, 0f, 0f));
            AddBake(firstBone, new Vector3(2f, 3f, 1f));
            var mesh = CreateMesh(new[] { new Vector3(5f, 0f, 0f), new Vector3(6f, 0f, 0f) },
                new[] { FullWeight(0), new BoneWeight { boneIndex0 = 0, weight0 = 0.5f, boneIndex1 = 1, weight1 = 0.5f } },
                new[] { firstBone.worldToLocalMatrix, secondBone.worldToLocalMatrix });
            var renderer = CreateSkinned(root, mesh, new[] { firstBone, secondBone });
            renderer.rootBone = root;
            renderer.localBounds = new Bounds(new Vector3(5.5f, 0f, 0f), new Vector3(1.1f, 0.1f, 0.1f));
            var originalBounds = renderer.localBounds;
            BakeAll(root);
            var positions = SkinVertices(renderer);
            var firstLocalPosition = root.worldToLocalMatrix.MultiplyPoint3x4(positions[0]);
            Assert.That(originalBounds.Contains(firstLocalPosition), Is.False);
            for (var index = 0; index < positions.Length; index++)
            {
                var local = root.worldToLocalMatrix.MultiplyPoint3x4(positions[index]);
                Assert.That(renderer.localBounds.Contains(local), Is.True);
            }
        }

        [Test]
        public void Bake_ResultHasErrors_DoesNotChangeMeshReferenceOrData()
        {
            var root = CreateObject("Root").transform;
            var bone = CreateChild(root, Vector3.zero);
            AddBake(bone, new Vector3(1f, 0f, 1f));
            var mesh = CreateMesh(new[] { Vector3.one }, new[] { FullWeight(0) }, new[] { bone.worldToLocalMatrix });
            var renderer = CreateSkinned(root, mesh, new[] { bone });
            var result = ScaleBakeSolver.Solve(root);
            var snapshot = ScaleBakeSnapshot.Capture(root);
            TransformBaker.Bake(root, result, snapshot);
            MeshBaker.Bake(root, result, snapshot);
            Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
            AssertVector(mesh.vertices[0], Vector3.one);
        }

        private void AssertSkinnedOracle(BoneWeight weight, bool outsideSecondBone)
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            var bakeFrame = CreateChild(bakeRoot, new Vector3(0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, 25f));
            var oracleFrame = CreateChild(oracleRoot, new Vector3(0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, 25f));
            var bakeFirst = CreateChild(bakeFrame, new Vector3(1f, 0f, 0f));
            var oracleFirst = CreateChild(oracleFrame, new Vector3(1f, 0f, 0f));
            var bakeSecond = outsideSecondBone ? CreateChild(bakeRoot, new Vector3(-1f, 1f, 0f)) : CreateChild(bakeFrame, new Vector3(-1f, 1f, 0f));
            var oracleSecond = outsideSecondBone ? CreateChild(oracleRoot, new Vector3(-1f, 1f, 0f)) : CreateChild(oracleFrame, new Vector3(-1f, 1f, 0f));
            AddBake(bakeFrame, new Vector3(2f, 3f, 1.5f));
            var mesh = CreateMesh(new[] { new Vector3(0.3f, 0.7f, -0.2f) }, new[] { weight },
                new[] { bakeFirst.worldToLocalMatrix, bakeSecond.worldToLocalMatrix });
            var oracleMesh = CreateMesh(mesh.vertices, mesh.boneWeights,
                new[] { oracleFirst.worldToLocalMatrix, oracleSecond.worldToLocalMatrix });
            var bakedRenderer = CreateSkinned(bakeRoot, mesh, new[] { bakeFirst, bakeSecond });
            var oracleRenderer = CreateSkinned(oracleRoot, oracleMesh, new[] { oracleFirst, oracleSecond });
            oracleFrame.localScale = new Vector3(2f, 3f, 1.5f);
            BakeAll(bakeRoot);
            AssertVector(SkinVertices(bakedRenderer)[0], SkinVertices(oracleRenderer)[0]);
        }

        private void BakeAll(Transform root)
        {
            var result = ScaleBakeSolver.Solve(root);
            var snapshot = ScaleBakeSnapshot.Capture(root);
            TransformBaker.Bake(root, result, snapshot);
            MeshBaker.Bake(root, result, snapshot);
            var meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var index = 0; index < meshes.Length; index++)
            {
                TrackGeneratedMesh(meshes[index].sharedMesh);
            }
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var index = 0; index < filters.Length; index++)
            {
                TrackGeneratedMesh(filters[index].sharedMesh);
            }
        }

        private void TrackGeneratedMesh(Mesh mesh)
        {
            if (mesh != null && !_objects.Contains(mesh))
            {
                _objects.Add(mesh);
            }
        }

        private Mesh CreateMesh(Vector3[] vertices, BoneWeight[] weights, Matrix4x4[] bindposes)
        {
            var normals = new Vector3[vertices.Length];
            var tangents = new Vector4[vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
            {
                normals[index] = Vector3.forward;
                tangents[index] = new Vector4(1f, 0f, 0f, 1f);
            }
            var mesh = new Mesh
            {
                name = "TestMesh",
                vertices = vertices,
                normals = normals,
                tangents = tangents,
                triangles = vertices.Length >= 3 ? new[] { 0, 1, 2 } : new int[0],
                boneWeights = weights,
                bindposes = bindposes
            };
            mesh.RecalculateBounds();
            _objects.Add(mesh);
            return mesh;
        }

        private SkinnedMeshRenderer CreateSkinned(Transform parent, Mesh mesh, Transform[] bones)
        {
            var renderer = CreateObject("Renderer").AddComponent<SkinnedMeshRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.localBounds = mesh.bounds;
            return renderer;
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            _objects.Add(value);
            return value;
        }

        private Transform CreateChild(Transform parent, Vector3 position, Quaternion? rotation = null)
        {
            var child = CreateObject("Child").transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localRotation = rotation ?? Quaternion.identity;
            return child;
        }

        private static void AddBake(Transform target, Vector3 scale)
        {
            target.gameObject.AddComponent<NonUniformScaleBake>().Scale = scale;
        }

        private static BoneWeight FullWeight(int index)
        {
            return new BoneWeight { boneIndex0 = index, weight0 = 1f };
        }

        private static Vector3[] SkinVertices(SkinnedMeshRenderer renderer)
        {
            var mesh = renderer.sharedMesh;
            var result = new Vector3[mesh.vertexCount];
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            for (var index = 0; index < vertices.Length; index++)
            {
                result[index] = SkinContribution(renderer, mesh, vertices[index], weights[index].boneIndex0, weights[index].weight0)
                    + SkinContribution(renderer, mesh, vertices[index], weights[index].boneIndex1, weights[index].weight1)
                    + SkinContribution(renderer, mesh, vertices[index], weights[index].boneIndex2, weights[index].weight2)
                    + SkinContribution(renderer, mesh, vertices[index], weights[index].boneIndex3, weights[index].weight3);
            }
            return result;
        }

        private static Vector3 SkinContribution(SkinnedMeshRenderer renderer, Mesh mesh, Vector3 vertex, int boneIndex, float weight)
        {
            if (weight == 0f)
            {
                return Vector3.zero;
            }
            return weight * (renderer.bones[boneIndex].localToWorldMatrix * mesh.bindposes[boneIndex]).MultiplyPoint3x4(vertex);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        private static void AssertMatrix(Matrix4x4 actual, Matrix4x4 expected)
        {
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(Tolerance));
                }
            }
        }
    }
}
