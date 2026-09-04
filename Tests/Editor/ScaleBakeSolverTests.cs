using System.Collections.Generic;
using net.bekobeko.utilitytools.core.scalebake;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class ScaleBakeSolverTests
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
        public void Solve_WithoutComponents_ReturnsEmptyResult()
        {
            var root = CreateObject("Root").transform;
            CreateChild(root, Vector3.zero);

            var result = ScaleBakeSolver.Solve(root);

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.AffectedTransforms, Is.Empty);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void Solve_SingleComponentInIdentityFrame_ReturnsScaleAsContentDeformation()
        {
            var root = CreateObject("Root").transform;
            var scale = new Vector3(2f, 3f, 4f);
            AddBake(root, scale);

            var result = ScaleBakeSolver.Solve(root);

            AssertMatrix(result.GetContentDeformation(root), Matrix4x4.Scale(scale));
        }

        [Test]
        public void Solve_TranslatedAndRotatedFrame_MatchesUnityTransformResult()
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            ConfigureTransform(bakeRoot, new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            ConfigureTransform(oracleRoot, new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            var bakeChild = CreateChild(bakeRoot, new Vector3(2f, 1f, -1f));
            var oracleChild = CreateChild(oracleRoot, new Vector3(2f, 1f, -1f));
            var originalPosition = bakeChild.position;
            var scale = new Vector3(2f, 3f, 1.5f);
            AddBake(bakeRoot, scale);
            oracleRoot.localScale = scale;

            var result = ScaleBakeSolver.Solve(bakeRoot);

            AssertVector(result.GetFrameDeformation(bakeChild).MultiplyPoint3x4(originalPosition), oracleChild.position);
        }

        [Test]
        public void Solve_NestedComponentsWithRotation_MatchesUnityTransformResult()
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            var bakeMiddle = CreateChild(bakeRoot, new Vector3(1f, 2f, 0f), Quaternion.Euler(20f, 35f, 10f));
            var oracleMiddle = CreateChild(oracleRoot, new Vector3(1f, 2f, 0f), Quaternion.Euler(20f, 35f, 10f));
            var bakeLeaf = CreateChild(bakeMiddle, new Vector3(0.5f, -1f, 2f));
            var oracleLeaf = CreateChild(oracleMiddle, new Vector3(0.5f, -1f, 2f));
            var originalPosition = bakeLeaf.position;
            var rootScale = new Vector3(2f, 1.5f, 1f);
            var middleScale = new Vector3(1f, 3f, 2f);
            AddBake(bakeRoot, rootScale);
            AddBake(bakeMiddle, middleScale);
            oracleRoot.localScale = rootScale;
            oracleMiddle.localScale = middleScale;

            var result = ScaleBakeSolver.Solve(bakeRoot);

            AssertVector(result.GetFrameDeformation(bakeLeaf).MultiplyPoint3x4(originalPosition), oracleLeaf.position);
        }

        [Test]
        public void Solve_ExistingUniformHierarchyScale_MatchesUnityAndHasNoIssues()
        {
            var bakeRoot = CreateObject("BakeRoot").transform;
            var oracleRoot = CreateObject("OracleRoot").transform;
            var bakeFrame = CreateChild(bakeRoot, new Vector3(1f, 0f, 0f));
            var oracleFrame = CreateChild(oracleRoot, new Vector3(1f, 0f, 0f));
            bakeFrame.localScale = Vector3.one * 0.9f;
            oracleFrame.localScale = Vector3.one * 0.9f;
            var bakeChild = CreateChild(bakeFrame, new Vector3(1f, 2f, 3f));
            var oracleChild = CreateChild(oracleFrame, new Vector3(1f, 2f, 3f));
            var originalPosition = bakeChild.position;
            var scale = new Vector3(2f, 3f, 4f);
            AddBake(bakeFrame, scale);
            oracleFrame.localScale = Vector3.Scale(oracleFrame.localScale, scale);

            var result = ScaleBakeSolver.Solve(bakeRoot);

            AssertVector(result.GetFrameDeformation(bakeChild).MultiplyPoint3x4(originalPosition), oracleChild.position);
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void Solve_ExistingNonUniformHierarchyScale_ReportsWarningAndReturnsResult()
        {
            var root = CreateObject("Root").transform;
            var frame = CreateChild(root, Vector3.zero);
            frame.localScale = new Vector3(1f, 2f, 1f);
            var child = CreateChild(frame, Vector3.one);
            AddBake(frame, new Vector3(2f, 3f, 4f));

            var result = ScaleBakeSolver.Solve(root);

            Assert.That(result.IsAffected(child), Is.True);
            AssertIssue(result, ScaleBakeIssueCode.HierarchyNonUniformScale, frame, ScaleBakeIssueSeverity.Warning);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void Solve_ZeroComponentScale_ReportsErrorAndIgnoresComponent()
        {
            var root = CreateObject("Root").transform;
            var child = CreateChild(root, Vector3.one);
            AddBake(root, new Vector3(1f, 0f, 2f));

            var result = ScaleBakeSolver.Solve(root);

            AssertIssue(result, ScaleBakeIssueCode.NonPositiveScale, root, ScaleBakeIssueSeverity.Error);
            Assert.That(result.HasErrors, Is.True);
            AssertMatrix(result.GetFrameDeformation(child), Matrix4x4.identity);
        }

        [Test]
        public void Solve_NegativeComponentScale_ReportsError()
        {
            var root = CreateObject("Root").transform;
            AddBake(root, new Vector3(1f, -1f, 2f));

            var result = ScaleBakeSolver.Solve(root);

            AssertIssue(result, ScaleBakeIssueCode.NonPositiveScale, root, ScaleBakeIssueSeverity.Error);
            Assert.That(result.HasErrors, Is.True);
        }

        [Test]
        public void Solve_InvalidComponentScale_ReportsOnlyInvalidErrorAndIgnoresComponent()
        {
            var root = CreateObject("Root").transform;
            var child = CreateChild(root, Vector3.one);
            AddBake(root, new Vector3(1f, float.NaN, 2f));

            var result = ScaleBakeSolver.Solve(root);

            AssertIssue(result, ScaleBakeIssueCode.InvalidScale, root, ScaleBakeIssueSeverity.Error);
            Assert.That(result.HasErrors, Is.True);
            Assert.That(HasIssue(result, ScaleBakeIssueCode.NonPositiveScale, root), Is.False);
            AssertMatrix(result.GetFrameDeformation(child), Matrix4x4.identity);
        }

        [Test]
        public void Solve_SingularHierarchyScale_ReportsOnlySingularErrorForTransform()
        {
            var root = CreateObject("Root").transform;
            root.localScale = new Vector3(1f, 0f, 1f);
            var child = CreateChild(root, Vector3.one);
            AddBake(child, new Vector3(2f, 3f, 4f));

            var result = ScaleBakeSolver.Solve(root);

            AssertIssue(result, ScaleBakeIssueCode.HierarchySingularScale, root, ScaleBakeIssueSeverity.Error);
            Assert.That(result.HasErrors, Is.True);
            Assert.That(HasIssue(result, ScaleBakeIssueCode.HierarchyNegativeScale, root), Is.False);
            Assert.That(HasIssue(result, ScaleBakeIssueCode.HierarchyNonUniformScale, root), Is.False);
        }

        [Test]
        public void Solve_NegativeNonUniformHierarchyScale_ReportsBothWarnings()
        {
            var root = CreateObject("Root").transform;
            root.localScale = new Vector3(1f, -2f, 1f);
            var child = CreateChild(root, Vector3.one);
            AddBake(child, new Vector3(2f, 3f, 4f));

            var result = ScaleBakeSolver.Solve(root);

            AssertIssue(result, ScaleBakeIssueCode.HierarchyNegativeScale, root, ScaleBakeIssueSeverity.Warning);
            AssertIssue(result, ScaleBakeIssueCode.HierarchyNonUniformScale, root, ScaleBakeIssueSeverity.Warning);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void Solve_ComponentNode_ExcludesOwnScaleFromFrameAndIncludesItInContent()
        {
            var root = CreateObject("Root").transform;
            var node = CreateChild(root, new Vector3(2f, 0f, 0f));
            var scale = new Vector3(2f, 3f, 4f);
            AddBake(node, scale);

            var result = ScaleBakeSolver.Solve(root);

            AssertMatrix(result.GetFrameDeformation(node), Matrix4x4.identity);
            var point = node.position + node.right;
            var transformedPoint = result.GetContentDeformation(node).MultiplyPoint3x4(point);
            AssertVector(transformedPoint, node.position + node.right * scale.x);
        }

        [Test]
        public void Solve_NonUniformScaleOutsideBakeRange_DoesNotReportWarning()
        {
            var root = CreateObject("Root").transform;
            var affectedBranch = CreateChild(root, Vector3.zero);
            var unaffectedBranch = CreateChild(root, Vector3.zero);
            unaffectedBranch.localScale = new Vector3(1f, 2f, 3f);
            AddBake(affectedBranch, new Vector3(2f, 3f, 4f));

            var result = ScaleBakeSolver.Solve(root);

            Assert.That(HasIssue(result, ScaleBakeIssueCode.HierarchyNonUniformScale, unaffectedBranch), Is.False);
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

        private static void AssertIssue(
            ScaleBakeSolveResult result,
            ScaleBakeIssueCode code,
            Transform target,
            ScaleBakeIssueSeverity severity)
        {
            for (var index = 0; index < result.Issues.Count; index++)
            {
                var issue = result.Issues[index];
                if (issue.Code == code && issue.Target == target && issue.Severity == severity) return;
            }

            Assert.Fail("指定された診断が見つかりませんでした。");
        }

        private static bool HasIssue(ScaleBakeSolveResult result, ScaleBakeIssueCode code, Transform target)
        {
            for (var index = 0; index < result.Issues.Count; index++)
            {
                if (result.Issues[index].Code == code && result.Issues[index].Target == target) return true;
            }
            return false;
        }
    }
}
