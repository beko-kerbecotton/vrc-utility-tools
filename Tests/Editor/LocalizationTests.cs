using System.Collections.Generic;
using net.bekobeko.utilitytools.localization;
using NUnit.Framework;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class LocalizationTests
    {
        [Test]
        public void Parse_ValidJson_ReturnsEntries()
        {
            const string json = "{\"entries\":[{\"key\":\"greeting\",\"value\":\"Hello\"}]}";

            var result = LocalizationCatalog.Parse(json);

            Assert.That(result["greeting"], Is.EqualTo("Hello"));
        }

        [Test]
        public void Parse_InvalidJson_ReturnsEmptyDictionary()
        {
            var result = LocalizationCatalog.Parse("{invalid");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Get_MissingSelectedTranslation_FallsBackToEnglish()
        {
            var english = new Dictionary<string, string> { ["greeting"] = "Hello" };
            var japanese = new Dictionary<string, string>();
            var catalog = new LocalizationCatalog(english, japanese);

            Assert.That(catalog.Get("greeting"), Is.EqualTo("Hello"));
        }

        [Test]
        public void Get_MissingEnglishTranslation_FallsBackToKey()
        {
            var catalog = new LocalizationCatalog(
                new Dictionary<string, string>(),
                new Dictionary<string, string>());

            Assert.That(catalog.Get("missing.key"), Is.EqualTo("missing.key"));
        }

        [TestCase("Editor/Localization/en.json")]
        [TestCase("Packages/net.bekobeko.utilitytools/Editor/Localization/ja.json")]
        [TestCase("Library/PackageCache/net.bekobeko.utilitytools@1.0.0/Editor/Localization/en.json")]
        [TestCase(@"Packages\net.bekobeko.utilitytools\Editor\Localization\JA.JSON")]
        public void IsLocalizationAssetPath_TargetTranslation_ReturnsTrue(string assetPath)
        {
            Assert.That(
                LocalizationCacheInvalidator.IsLocalizationAssetPath(assetPath),
                Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Packages/another.package/Localization/en.json")]
        [TestCase("Packages/another.package/SomeEditor/Localization/en.json")]
        [TestCase("Packages/net.bekobeko.utilitytools/Editor/Localization/notes.json")]
        [TestCase("Packages/net.bekobeko.utilitytools/Editor/Localization/en.json.meta")]
        public void IsLocalizationAssetPath_UnrelatedAsset_ReturnsFalse(string assetPath)
        {
            Assert.That(
                LocalizationCacheInvalidator.IsLocalizationAssetPath(assetPath),
                Is.False);
        }

        [Test]
        public void SelectFallbackAssetPath_OtherPackageListedFirst_PrefersThisPackage()
        {
            var candidates = new[]
            {
                "Packages/another.package/Editor/Localization/en.json",
                "Packages/net.bekobeko.utilitytools/Editor/Localization/en.json"
            };

            var result = EditorLocalization.SelectFallbackAssetPath(
                candidates,
                "Editor/Localization/en.json");

            Assert.That(
                result,
                Is.EqualTo("Packages/net.bekobeko.utilitytools/Editor/Localization/en.json"));
        }

        [Test]
        public void SelectFallbackAssetPath_PackageCachePath_PrefersThisPackage()
        {
            var candidates = new[]
            {
                "Packages/another.package/Editor/Localization/ja.json",
                "Library/PackageCache/net.bekobeko.utilitytools@1.0.0/Editor/Localization/ja.json"
            };

            var result = EditorLocalization.SelectFallbackAssetPath(
                candidates,
                "Editor/Localization/ja.json");

            Assert.That(
                result,
                Is.EqualTo(
                    "Library/PackageCache/net.bekobeko.utilitytools@1.0.0/Editor/Localization/ja.json"));
        }

        [Test]
        public void SelectFallbackAssetPath_WithoutPackageName_ReturnsFirstRelativeMatch()
        {
            var candidates = new[]
            {
                "Assets/BekoUtilityTools/Editor/Localization/en.json",
                "Assets/Copy/Editor/Localization/en.json"
            };

            var result = EditorLocalization.SelectFallbackAssetPath(
                candidates,
                "Editor/Localization/en.json");

            Assert.That(
                result,
                Is.EqualTo("Assets/BekoUtilityTools/Editor/Localization/en.json"));
        }

        [Test]
        public void SelectFallbackAssetPath_IgnoresNonMatchingRelativePath()
        {
            var candidates = new[]
            {
                "Packages/net.bekobeko.utilitytools/SomeEditor/Localization/en.json",
                "Assets/BekoUtilityTools/Editor/Localization/ja.json"
            };

            var result = EditorLocalization.SelectFallbackAssetPath(
                candidates,
                "Editor/Localization/en.json");

            Assert.That(result, Is.Null);
        }
    }
}
