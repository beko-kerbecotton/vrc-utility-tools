using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public static class MaterialSwapCandidateFinder
    {
        public static List<Material> BuildCandidateList(QuickSwapMode mode, Material baseMaterial)
        {
            switch (mode)
            {
                case QuickSwapMode.SameDirectory:
                    return FindSameDirectory(baseMaterial);
                case QuickSwapMode.SiblingDirectory:
                    return FindSiblingDirectories(baseMaterial);
                default:
                    return new List<Material>();
            }
        }

        public static int LevenshteinDistance(string first, string second)
        {
            first = first ?? string.Empty;
            second = second ?? string.Empty;
            if (first.Length == 0) return second.Length;
            if (second.Length == 0) return first.Length;

            var previous = new int[second.Length + 1];
            var current = new int[second.Length + 1];
            for (var index = 0; index <= second.Length; index++) previous[index] = index;

            for (var firstIndex = 1; firstIndex <= first.Length; firstIndex++)
            {
                current[0] = firstIndex;
                for (var secondIndex = 1; secondIndex <= second.Length; secondIndex++)
                {
                    var substitutionCost = first[firstIndex - 1] == second[secondIndex - 1] ? 0 : 1;
                    current[secondIndex] = Math.Min(
                        Math.Min(current[secondIndex - 1] + 1, previous[secondIndex] + 1),
                        previous[secondIndex - 1] + substitutionCost);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[second.Length];
        }

        private static List<Material> FindSameDirectory(Material baseMaterial)
        {
            var result = new List<Material>();
            if (!TryGetAssetLocation(baseMaterial, out _, out var directory)) return result;

            var paths = FindMaterialPaths(directory);
            foreach (var path in paths)
            {
                if (!string.Equals(GetDirectoryName(path), directory, StringComparison.Ordinal)) continue;
                AddMaterial(result, path);
            }

            return result;
        }

        private static List<Material> FindSiblingDirectories(Material baseMaterial)
        {
            var result = new List<Material>();
            if (!TryGetAssetLocation(baseMaterial, out var currentPath, out var currentDirectory)) return result;

            var parentDirectory = GetDirectoryName(currentDirectory);
            if (string.IsNullOrEmpty(parentDirectory)) return result;

            var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var path in FindMaterialPaths(parentDirectory))
            {
                var directory = GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) ||
                    !string.Equals(GetDirectoryName(directory), parentDirectory, StringComparison.Ordinal))
                    continue;

                if (!groups.TryGetValue(directory, out var group))
                {
                    group = new List<string>();
                    groups.Add(directory, group);
                }
                group.Add(path);
            }

            var selectedPaths = new List<string>();
            var currentFileName = Path.GetFileName(currentPath);
            foreach (var group in groups.Values)
            {
                group.Sort(StringComparer.Ordinal);
                var selected = group[0];
                var selectedDistance = LevenshteinDistance(Path.GetFileName(selected), currentFileName);
                for (var index = 1; index < group.Count; index++)
                {
                    var distance = LevenshteinDistance(Path.GetFileName(group[index]), currentFileName);
                    if (distance < selectedDistance)
                    {
                        selected = group[index];
                        selectedDistance = distance;
                    }
                }
                selectedPaths.Add(selected);
            }

            selectedPaths.Sort(StringComparer.Ordinal);
            foreach (var path in selectedPaths) AddMaterial(result, path);
            return result;
        }

        private static List<string> FindMaterialPaths(string directory)
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { directory }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static bool TryGetAssetLocation(
            Material material,
            out string assetPath,
            out string directory)
        {
            assetPath = material == null ? string.Empty : AssetDatabase.GetAssetPath(material);
            directory = string.IsNullOrEmpty(assetPath) ? null : GetDirectoryName(assetPath);
            return !string.IsNullOrEmpty(directory);
        }

        private static string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        private static void AddMaterial(List<Material> result, string assetPath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null) result.Add(material);
        }
    }
}
