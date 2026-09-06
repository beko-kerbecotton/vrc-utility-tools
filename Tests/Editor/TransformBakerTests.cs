using System.Collections.Generic;
using net.bekobeko.utilitytools.core.scalebake;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class TransformBakerTests
    {
        private const float Tolerance = 1e-4f;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void Bake_SingleTranslatedAndRotatedFrame_MatchesUnityTransformResult()
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            ConfigureTransform(bakeRoot, new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 90f), Vector3.one);
            ConfigureTransform(oracleRoot, new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 90f), Vector3.one);
            var bakeChild = CreateChild(bakeRoot, new Vector3(2f, 1f, -1f));
            var oracleChild = CreateChild(oracleRoot, new Vector3(2f, 1f, -1f));
            var scale = new Vector3(2f, 3f, 1.5f);
            AddBake(bakeRoot, scale);
            oracleRoot.localScale = scale;

            TransformBaker.Bake(bakeRoot, ScaleBakeSolver.Solve(bakeRoot));

            AssertVector(bakeChild.position, oracleChild.position);
        }

        [Test]
        public void Bake_NestedComponentsWithRotation_MatchesUnityTransformResult()
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            var bakeMiddle = CreateChild(bakeRoot, new Vector3(1f, 2f, 0f), Quaternion.Euler(20f, 35f, 10f));
            var oracleMiddle = CreateChild(oracleRoot, new Vector3(1f, 2f, 0f), Quaternion.Euler(20f, 35f, 10f));
            var bakeLeaf = CreateChild(bakeMiddle, new Vector3(0.5f, -1f, 2f));
            var oracleLeaf = CreateChild(oracleMiddle, new Vector3(0.5f, -1f, 2f));
            var rootScale = new Vector3(2f, 1.5f, 1f);
            var middleScale = new Vector3(1f, 3f, 2f);
            AddBake(bakeRoot, rootScale);
            AddBake(bakeMiddle, middleScale);
            oracleRoot.localScale = rootScale;
            oracleMiddle.localScale = middleScale;

            TransformBaker.Bake(bakeRoot, ScaleBakeSolver.Solve(bakeRoot));

            AssertVector(bakeMiddle.position, oracleMiddle.position);
            AssertVector(bakeLeaf.position, oracleLeaf.position);
        }

        [Test]
        public void Bake_ExistingUniformScale_PreservesEveryLocalScale()
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            var bakeFrame = CreateChild(bakeRoot, new Vector3(1f, 0f, 0f));
            var oracleFrame = CreateChild(oracleRoot, new Vector3(1f, 0f, 0f));
            bakeFrame.localScale = Vector3.one * 0.9f;
            oracleFrame.localScale = Vector3.one * 0.9f;
            var bakeChild = CreateChild(bakeFrame, new Vector3(1f, 2f, 3f));
            var oracleChild = CreateChild(oracleFrame, new Vector3(1f, 2f, 3f));
            var bakeLeaf = CreateChild(bakeChild, new Vector3(-1f, 0.5f, 2f));
            var oracleLeaf = CreateChild(oracleChild, new Vector3(-1f, 0.5f, 2f));
            bakeChild.localScale = Vector3.one * 1.1f;
            oracleChild.localScale = bakeChild.localScale;
            var scale = new Vector3(2f, 3f, 4f);
            AddBake(bakeFrame, scale);
            oracleFrame.localScale = Vector3.Scale(oracleFrame.localScale, scale);
            var transforms = new[] { bakeRoot, bakeFrame, bakeChild, bakeLeaf };
            var originalScales = CaptureLocalScales(transforms);

            TransformBaker.Bake(bakeRoot, ScaleBakeSolver.Solve(bakeRoot));

            AssertVector(bakeChild.position, oracleChild.position);
            AssertVector(bakeLeaf.position, oracleLeaf.position);
            Assert.That(bakeFrame.localScale, Is.EqualTo(Vector3.one * 0.9f));
            AssertLocalScales(transforms, originalScales);
        }

        [Test]
        public void Bake_PreservesEveryLocalRotation()
        {
            var root = CreateObject("Root").transform;
            root.localRotation = Quaternion.Euler(5f, 10f, 15f);
            var frame = CreateChild(root, new Vector3(1f, 2f, 3f), Quaternion.Euler(20f, 30f, 40f));
            var child = CreateChild(frame, new Vector3(2f, -1f, 0.5f), Quaternion.Euler(45f, 25f, 5f));
            AddBake(frame, new Vector3(2f, 3f, 1.5f));
            var transforms = new[] { root, frame, child };
            var originalRotations = CaptureLocalRotations(transforms);

            TransformBaker.Bake(root, ScaleBakeSolver.Solve(root));

            AssertLocalRotations(transforms, originalRotations);
        }

        [Test]
        public void Bake_ComponentOrigin_DoesNotMove()
        {
            var root = CreateObject("Root").transform;
            var frame = CreateChild(root, new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f));
            CreateChild(frame, new Vector3(2f, 1f, -1f));
            AddBake(frame, new Vector3(2f, 3f, 4f));
            var originalLocalPosition = frame.localPosition;
            var originalPosition = frame.position;

            TransformBaker.Bake(root, ScaleBakeSolver.Solve(root));

            Assert.That(frame.localPosition, Is.EqualTo(originalLocalPosition));
            Assert.That(frame.position, Is.EqualTo(originalPosition));
        }

        [Test]
        public void Bake_UnaffectedSiblingBranch_DoesNotMove()
        {
            var root = CreateObject("Root").transform;
            var affected = CreateChild(root, new Vector3(1f, 0f, 0f));
            CreateChild(affected, new Vector3(2f, 3f, 4f));
            var unaffected = CreateChild(root, new Vector3(-2f, 1f, 3f), Quaternion.Euler(15f, 25f, 35f));
            var unaffectedChild = CreateChild(unaffected, new Vector3(0.5f, -1f, 2f));
            AddBake(affected, new Vector3(2f, 3f, 4f));
            var originalLocalPosition = unaffected.localPosition;
            var originalPosition = unaffected.position;
            var originalChildLocalPosition = unaffectedChild.localPosition;
            var originalChildPosition = unaffectedChild.position;

            TransformBaker.Bake(root, ScaleBakeSolver.Solve(root));

            Assert.That(unaffected.localPosition, Is.EqualTo(originalLocalPosition));
            Assert.That(unaffected.position, Is.EqualTo(originalPosition));
            Assert.That(unaffectedChild.localPosition, Is.EqualTo(originalChildLocalPosition));
            Assert.That(unaffectedChild.position, Is.EqualTo(originalChildPosition));
        }

        [Test]
        public void Bake_ResultHasErrors_DoesNotChangeTransforms()
        {
            var root = CreateObject("Root").transform;
            ConfigureTransform(root, new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f), Vector3.one * 0.9f);
            var child = CreateChild(root, new Vector3(2f, -1f, 0.5f), Quaternion.Euler(15f, 25f, 35f));
            child.localScale = Vector3.one * 1.1f;
            AddBake(root, new Vector3(2f, 0f, 3f));
            var transforms = new[] { root, child };
            var localPositions = CaptureLocalPositions(transforms);
            var localRotations = CaptureLocalRotations(transforms);
            var localScales = CaptureLocalScales(transforms);

            var result = ScaleBakeSolver.Solve(root);
            TransformBaker.Bake(root, result);

            Assert.That(result.HasErrors, Is.True);
            AssertLocalPositions(transforms, localPositions);
            AssertLocalRotations(transforms, localRotations);
            AssertLocalScales(transforms, localScales);
        }

        [Test]
        public void Bake_WithoutComponents_DoesNotChangeTransforms()
        {
            var root = CreateObject("Root").transform;
            ConfigureTransform(root, new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f), Vector3.one * 0.9f);
            var child = CreateChild(root, new Vector3(2f, -1f, 0.5f), Quaternion.Euler(15f, 25f, 35f));
            child.localScale = Vector3.one * 1.1f;
            var transforms = new[] { root, child };
            var localPositions = CaptureLocalPositions(transforms);
            var localRotations = CaptureLocalRotations(transforms);
            var localScales = CaptureLocalScales(transforms);

            TransformBaker.Bake(root, ScaleBakeSolver.Solve(root));

            AssertLocalPositions(transforms, localPositions);
            AssertLocalRotations(transforms, localRotations);
            AssertLocalScales(transforms, localScales);
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        private Transform CreateChild(Transform parent, Vector3 localPosition, Quaternion? localRotation = null)
        {
            var child = CreateObject("Child").transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = localRotation ?? Quaternion.identity;
            return child;
        }

        private static void ConfigureTransform(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = scale;
        }

        private static void AddBake(Transform transform, Vector3 scale)
        {
            transform.gameObject.AddComponent<NonUniformScaleBake>().Scale = scale;
        }

        private static Vector3[] CaptureLocalPositions(Transform[] transforms)
        {
            var values = new Vector3[transforms.Length];
            for (var index = 0; index < transforms.Length; index++) values[index] = transforms[index].localPosition;
            return values;
        }

        private static Quaternion[] CaptureLocalRotations(Transform[] transforms)
        {
            var values = new Quaternion[transforms.Length];
            for (var index = 0; index < transforms.Length; index++) values[index] = transforms[index].localRotation;
            return values;
        }

        private static Vector3[] CaptureLocalScales(Transform[] transforms)
        {
            var values = new Vector3[transforms.Length];
            for (var index = 0; index < transforms.Length; index++) values[index] = transforms[index].localScale;
            return values;
        }

        private static void AssertLocalPositions(Transform[] transforms, Vector3[] expected)
        {
            Assert.That(transforms.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < transforms.Length; index++)
            {
                Assert.That(transforms[index].localPosition, Is.EqualTo(expected[index]));
            }
        }

        private static void AssertLocalRotations(Transform[] transforms, Quaternion[] expected)
        {
            Assert.That(transforms.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < transforms.Length; index++)
            {
                Assert.That(transforms[index].localRotation, Is.EqualTo(expected[index]));
            }
        }

        private static void AssertLocalScales(Transform[] transforms, Vector3[] expected)
        {
            Assert.That(transforms.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < transforms.Length; index++)
            {
                Assert.That(transforms[index].localScale, Is.EqualTo(expected[index]));
            }
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
