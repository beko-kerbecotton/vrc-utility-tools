using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.localization
{
    public enum ToolLanguage
    {
        English,
        Japanese
    }

    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class LocalizationResource
    {
        public LocalizationEntry[] entries;
    }

    public sealed class LocalizationCatalog
    {
        private readonly IReadOnlyDictionary<string, string> _english;
        private readonly IReadOnlyDictionary<string, string> _selected;

        public LocalizationCatalog(
            IReadOnlyDictionary<string, string> english,
            IReadOnlyDictionary<string, string> selected)
        {
            _english = english ?? new Dictionary<string, string>();
            _selected = selected ?? new Dictionary<string, string>();
        }

        public string Get(string key)
        {
            if (!string.IsNullOrEmpty(key) && _selected.TryGetValue(key, out var selectedValue))
                return selectedValue;
            if (!string.IsNullOrEmpty(key) && _english.TryGetValue(key, out var englishValue))
                return englishValue;
            return key ?? string.Empty;
        }

        public static IReadOnlyDictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                var resource = JsonUtility.FromJson<LocalizationResource>(json);
                if (resource?.entries == null) return result;

                foreach (var entry in resource.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null) continue;
                    result[entry.key] = entry.value;
                }
            }
            catch (ArgumentException)
            {
                // A malformed optional translation must not break the editor window.
            }

            return result;
        }
    }

    public static class EditorLocalization
    {
        private const string PackageName = "net.bekobeko.utilitytools";
        private const string LanguagePreferenceKey = "net.bekobeko.utilitytools.language";
        private static readonly string[] LanguageNames = { "English", "日本語" };
        private static readonly IReadOnlyDictionary<string, string> BuiltInEnglish =
            new Dictionary<string, string>
            {
                ["common.language"] = "Language",
                ["common.ok"] = "OK"
            };

        private static ToolLanguage? _loadedLanguage;
        private static LocalizationCatalog _catalog;

        public static ToolLanguage Language
        {
            get
            {
                var stored = EditorPrefs.GetInt(LanguagePreferenceKey, (int)ToolLanguage.English);
                return Enum.IsDefined(typeof(ToolLanguage), stored)
                    ? (ToolLanguage)stored
                    : ToolLanguage.English;
            }
            set
            {
                EditorPrefs.SetInt(LanguagePreferenceKey, (int)value);
                InvalidateCache();
            }
        }

        public static void InvalidateCache()
        {
            _catalog = null;
            _loadedLanguage = null;
        }

        public static string Get(string key)
        {
            EnsureLoaded();
            return _catalog.Get(key);
        }

        public static string Format(string key, params object[] arguments)
        {
            try
            {
                return string.Format(Get(key), arguments);
            }
            catch (FormatException)
            {
                return Get(key);
            }
        }

        public static bool DrawLanguageSelector()
        {
            var current = Language;
            var selected = (ToolLanguage)EditorGUILayout.Popup(Get("common.language"), (int)current, LanguageNames);
            if (selected == current) return false;

            Language = selected;
            return true;
        }

        public static string SelectFallbackAssetPath(
            IEnumerable<string> candidatePaths,
            string relativePath)
        {
            if (candidatePaths == null || string.IsNullOrEmpty(relativePath)) return null;

            var normalizedRelativePath = relativePath.Replace('\\', '/').TrimStart('/');
            if (normalizedRelativePath.Length == 0) return null;

            string firstRelativeMatch = null;
            foreach (var candidatePath in candidatePaths)
            {
                if (string.IsNullOrEmpty(candidatePath)) continue;

                var normalizedCandidatePath = candidatePath.Replace('\\', '/');
                if (!normalizedCandidatePath.Equals(
                        normalizedRelativePath,
                        StringComparison.OrdinalIgnoreCase) &&
                    !normalizedCandidatePath.EndsWith(
                        "/" + normalizedRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (firstRelativeMatch == null) firstRelativeMatch = candidatePath;
                if (IsPackagePath(normalizedCandidatePath)) return candidatePath;
            }

            return firstRelativeMatch;
        }

        private static void EnsureLoaded()
        {
            var language = Language;
            if (_catalog != null && _loadedLanguage == language) return;

            var english = Merge(BuiltInEnglish, Load("en"));
            var selected = language == ToolLanguage.English ? english : Load("ja");
            _catalog = new LocalizationCatalog(english, selected);
            _loadedLanguage = language;
        }

        private static IReadOnlyDictionary<string, string> Load(string languageCode)
        {
            var assetPath = GetPackageAssetPath($"Editor/Localization/{languageCode}.json");
            var asset = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            return LocalizationCatalog.Parse(asset != null ? asset.text : null);
        }

        private static string GetPackageAssetPath(string relativePath)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(EditorLocalization).Assembly);
            if (package != null) return $"{package.assetPath}/{relativePath}";

            var fileName = System.IO.Path.GetFileNameWithoutExtension(relativePath);
            var candidatePaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets($"{fileName} t:TextAsset"))
                candidatePaths.Add(AssetDatabase.GUIDToAssetPath(guid));

            return SelectFallbackAssetPath(candidatePaths, relativePath);
        }

        private static bool IsPackagePath(string normalizedPath)
        {
            foreach (var segment in normalizedPath.Split('/'))
            {
                if (segment.Equals(PackageName, StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith(PackageName + "@", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IReadOnlyDictionary<string, string> Merge(
            IReadOnlyDictionary<string, string> baseline,
            IReadOnlyDictionary<string, string> overrides)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in baseline) result[pair.Key] = pair.Value;
            foreach (var pair in overrides) result[pair.Key] = pair.Value;
            return result;
        }
    }
}
