using System;
using UnityEditor;

namespace net.bekobeko.utilitytools.localization
{
    public sealed class LocalizationCacheInvalidator : AssetPostprocessor
    {
        private static readonly string[] LocalizationAssetPaths =
        {
            "Editor/Localization/en.json",
            "Editor/Localization/ja.json"
        };

        public static bool IsLocalizationAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            var normalizedPath = assetPath.Replace('\\', '/');
            foreach (var relativePath in LocalizationAssetPaths)
            {
                if (normalizedPath.Equals(relativePath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.EndsWith("/" + relativePath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsLocalizationAsset(importedAssets) ||
                ContainsLocalizationAsset(deletedAssets) ||
                ContainsLocalizationAsset(movedAssets) ||
                ContainsLocalizationAsset(movedFromAssetPaths))
                EditorLocalization.InvalidateCache();
        }

        private static bool ContainsLocalizationAsset(string[] assetPaths)
        {
            if (assetPaths == null) return false;
            foreach (var assetPath in assetPaths)
            {
                if (IsLocalizationAssetPath(assetPath)) return true;
            }

            return false;
        }
    }
}
