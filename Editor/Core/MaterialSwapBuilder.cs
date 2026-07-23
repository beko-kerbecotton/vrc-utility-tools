using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public static class MaterialSwapBuilder
    {
        public static ComponentBuildStatus Build(
            GameObject componentTarget,
            GameObject targetRoot,
            IEnumerable<MaterialSwapEntry> entries,
            QuickSwapMode mode = QuickSwapMode.None,
            ExistingComponentHandlingMode handlingMode = ExistingComponentHandlingMode.Skip,
            ConflictResolutionSession conflictSession = null)
        {
            if (componentTarget == null || targetRoot == null || entries == null)
                return ComponentBuildStatus.InvalidTarget;

            var generatedCommands = new List<MatSwap>();
            foreach (var entry in entries)
            {
                if (entry?.Original == null || !entry.Enabled) continue;
                generatedCommands.Add(new MatSwap { From = entry.Original, To = entry.Replacement });
            }

            var generatedSwaps = generatedCommands.FindAll(swap => swap.To != null);

            var existing = componentTarget.GetComponent<ModularAvatarMaterialSwap>();
            if (existing != null && handlingMode == ExistingComponentHandlingMode.Skip)
                return ComponentBuildStatus.AlreadyExists;

            var generatedRoot = new AvatarObjectReference(targetRoot);
            if (existing == null)
            {
                if (generatedSwaps.Count == 0) return ComponentBuildStatus.NoAssignments;
                var component = Undo.AddComponent<ModularAvatarMaterialSwap>(componentTarget);
                component.Root = generatedRoot;
                component.Swaps = generatedSwaps;
                component.QuickSwapMode = mode;
                EditorUtility.SetDirty(component);
                return ComponentBuildStatus.Success;
            }

            if (handlingMode == ExistingComponentHandlingMode.Overwrite)
                return UpdateExisting(existing, generatedRoot, mode, generatedSwaps);

            var mergedRoot = existing.Root;
            var mergedMode = existing.QuickSwapMode;
            var mergedSwaps = new List<MatSwap>(existing.Swaps ?? new List<MatSwap>());
            var changed = false;

            if (!AvatarObjectReferenceComparer.AreEqual(mergedRoot, generatedRoot))
            {
                var action = Resolve(
                    conflictSession,
                    new ConflictInfo(
                        "Root",
                        FormatReference(mergedRoot),
                        FormatReference(generatedRoot)));
                if (action == ConflictResolutionAction.Cancel) return ComponentBuildStatus.Cancelled;
                if (action == ConflictResolutionAction.Overwrite)
                {
                    mergedRoot = generatedRoot;
                    changed = true;
                }
            }

            if (mergedMode != mode)
            {
                var action = Resolve(
                    conflictSession,
                    new ConflictInfo("Quick Swap Mode", mergedMode.ToString(), mode.ToString()));
                if (action == ConflictResolutionAction.Cancel) return ComponentBuildStatus.Cancelled;
                if (action == ConflictResolutionAction.Overwrite)
                {
                    mergedMode = mode;
                    changed = true;
                }
            }

            var swapMergeResult = MergeListHelper.MergeList(
                mergedSwaps,
                generatedCommands,
                (current, generated) => current.From == generated.From,
                (current, generated) => current.To == generated.To,
                (current, generated) => new ConflictInfo(
                    $"Material: {FormatMaterial(generated.From)}",
                    FormatMaterial(current.To),
                    generated.To != null ? FormatMaterial(generated.To) : "<none> (delete)"),
                conflictSession,
                generated => generated.To == null);
            if (swapMergeResult.Status == MergeStatus.Cancelled)
                return ComponentBuildStatus.Cancelled;
            if (swapMergeResult.Status == MergeStatus.Changed)
            {
                mergedSwaps = swapMergeResult.Items;
                changed = true;
            }

            return changed
                ? UpdateExisting(existing, mergedRoot, mergedMode, mergedSwaps)
                : ComponentBuildStatus.NoChanges;
        }

        private static ConflictResolutionAction Resolve(
            ConflictResolutionSession session,
            ConflictInfo conflict)
        {
            return session?.Resolve(conflict) ?? ConflictResolutionAction.Skip;
        }

        private static ComponentBuildStatus UpdateExisting(
            ModularAvatarMaterialSwap component,
            AvatarObjectReference root,
            QuickSwapMode mode,
            List<MatSwap> swaps)
        {
            Undo.RecordObject(component, "Update MA Material Swap");
            component.Root = root;
            component.QuickSwapMode = mode;
            component.Swaps = swaps;
            EditorUtility.SetDirty(component);
            return ComponentBuildStatus.Updated;
        }

        private static string FormatReference(AvatarObjectReference reference)
        {
            if (reference == null) return "<none>";
            return string.IsNullOrEmpty(reference.referencePath)
                ? "<object>"
                : reference.referencePath;
        }

        private static string FormatMaterial(Material material)
        {
            return material != null ? material.name : "<none>";
        }
    }
}
