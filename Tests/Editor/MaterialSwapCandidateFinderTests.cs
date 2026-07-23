using System.Linq;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class MaterialSwapCandidateFinderTests
    {
        private const string TestRoot = "Assets/__BekoUtilityToolsMaterialSwapTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "__BekoUtilityToolsMaterialSwapTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void SameDirectory_ReturnsOnlyDirectMaterialsInPathOrder()
        {
            CreateFolder(TestRoot, "Current");
            CreateFolder(TestRoot + "/Current", "Nested");
            var second = CreateMaterial(TestRoot + "/Current/B.mat");
            var first = CreateMaterial(TestRoot + "/Current/A.mat");
            CreateMaterial(TestRoot + "/Current/Nested/C.mat");

            var candidates = MaterialSwapCandidateFinder.BuildCandidateList(
                QuickSwapMode.SameDirectory, second);

            Assert.That(candidates, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void SiblingDirectory_ReturnsClosestFileFromEachSiblingInPathOrder()
        {
            CreateFolder(TestRoot, "Blue");
            CreateFolder(TestRoot, "Green");
            CreateFolder(TestRoot, "Red");
            var current = CreateMaterial(TestRoot + "/Blue/Body.mat");
            var green = CreateMaterial(TestRoot + "/Green/Other.mat");
            var redClosest = CreateMaterial(TestRoot + "/Red/BodyA.mat");
            CreateMaterial(TestRoot + "/Red/Unrelated.mat");

            var candidates = MaterialSwapCandidateFinder.BuildCandidateList(
                QuickSwapMode.SiblingDirectory, current);

            Assert.That(candidates, Is.EqualTo(new[] { current, green, redClosest }));
        }

        [Test]
        public void SiblingDirectory_UsesOrdinalPathToBreakDistanceTie()
        {
            CreateFolder(TestRoot, "Current");
            CreateFolder(TestRoot, "Sibling");
            var current = CreateMaterial(TestRoot + "/Current/Body.mat");
            var expected = CreateMaterial(TestRoot + "/Sibling/BodyA.mat");
            CreateMaterial(TestRoot + "/Sibling/BodyB.mat");

            var candidates = MaterialSwapCandidateFinder.BuildCandidateList(
                QuickSwapMode.SiblingDirectory, current);

            Assert.That(
                AssetDatabase.GetAssetPath(candidates.Last()),
                Is.EqualTo(AssetDatabase.GetAssetPath(expected)));
        }

        [TestCase("", "abc", 3)]
        [TestCase("kitten", "sitting", 3)]
        [TestCase("Body.mat", "Body.mat", 0)]
        public void LevenshteinDistance_ReturnsExpectedValue(string first, string second, int expected)
        {
            Assert.That(MaterialSwapCandidateFinder.LevenshteinDistance(first, second), Is.EqualTo(expected));
        }

        [Test]
        public void UnsavedMaterial_ReturnsNoCandidates()
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(
                    MaterialSwapCandidateFinder.BuildCandidateList(
                        QuickSwapMode.SiblingDirectory, material),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void CreateFolder(string parent, string name)
        {
            Assert.That(AssetDatabase.CreateFolder(parent, name), Is.Not.Empty);
        }

        private static Material CreateMaterial(string path)
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
