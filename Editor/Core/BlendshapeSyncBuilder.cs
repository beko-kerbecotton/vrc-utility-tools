using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public sealed class BlendshapeSyncBuildResult
    {
        public int AddedCount { get; internal set; }
        public int UpdatedCount { get; internal set; }
        public int UnchangedCount { get; internal set; }
        public bool Cancelled { get; internal set; }
        public IReadOnlyList<GameObject> ExistingComponentTargets => _existingComponentTargets;
        private readonly List<GameObject> _existingComponentTargets = new List<GameObject>();

        internal void AddExistingTarget(GameObject target)
        {
            _existingComponentTargets.Add(target);
        }
    }

    public static class BlendshapeSyncBuilder
    {
        private sealed class UpdatePlan
        {
            public GameObject Target { get; }
            public ModularAvatarBlendshapeSync Existing { get; }
            public List<BlendshapeBinding> Bindings { get; }

            public UpdatePlan(
                GameObject target,
                ModularAvatarBlendshapeSync existing,
                List<BlendshapeBinding> bindings)
            {
                Target = target;
                Existing = existing;
                Bindings = bindings;
            }
        }

        public static BlendshapeSyncBuildResult Build(
            BlendshapeMappingModel model,
            ExistingComponentHandlingMode handlingMode = ExistingComponentHandlingMode.Skip,
            ConflictResolutionSession conflictSession = null)
        {
            var result = new BlendshapeSyncBuildResult();
            if (model?.ReferenceRenderer == null) return result;

            var plans = new List<UpdatePlan>();
            foreach (var rendererEntry in model.Renderers)
            {
                if (!rendererEntry.Included || rendererEntry.Renderer == null) continue;
                var generatedCommands = BuildBindings(model.ReferenceRenderer, rendererEntry);
                var generatedBindings = generatedCommands.FindAll(binding =>
                    !string.IsNullOrEmpty(binding.Blendshape));

                var target = rendererEntry.Renderer.gameObject;
                var existing = target.GetComponent<ModularAvatarBlendshapeSync>();
                if (existing == null)
                {
                    if (generatedBindings.Count == 0) continue;
                    plans.Add(new UpdatePlan(target, null, generatedBindings));
                    continue;
                }

                if (handlingMode == ExistingComponentHandlingMode.Skip)
                {
                    result.AddExistingTarget(target);
                    continue;
                }

                if (handlingMode == ExistingComponentHandlingMode.Overwrite)
                {
                    plans.Add(new UpdatePlan(target, existing, generatedBindings));
                    continue;
                }

                var mergeStatus = TryMerge(
                    target,
                    existing.Bindings,
                    generatedCommands,
                    conflictSession,
                    out var mergedBindings);
                if (mergeStatus == MergeStatus.Cancelled)
                {
                    result.Cancelled = true;
                    return result;
                }
                if (mergeStatus == MergeStatus.Unchanged)
                {
                    result.UnchangedCount++;
                    continue;
                }

                plans.Add(new UpdatePlan(target, existing, mergedBindings));
            }

            ApplyPlans(plans, result);
            return result;
        }

        private static List<BlendshapeBinding> BuildBindings(
            SkinnedMeshRenderer referenceRenderer,
            BlendshapeRendererEntry rendererEntry)
        {
            var bindings = new List<BlendshapeBinding>();
            foreach (var mapping in rendererEntry.Mappings.Values)
            {
                if (!mapping.Enabled) continue;
                bindings.Add(new BlendshapeBinding
                {
                    ReferenceMesh = new AvatarObjectReference(referenceRenderer.gameObject),
                    Blendshape = mapping.ReferenceBlendshape,
                    LocalBlendshape = mapping.LocalBlendshape
                });
            }
            return bindings;
        }

        private static MergeStatus TryMerge(
            GameObject target,
            IReadOnlyList<BlendshapeBinding> existingBindings,
            IReadOnlyList<BlendshapeBinding> generatedBindings,
            ConflictResolutionSession conflictSession,
            out List<BlendshapeBinding> mergedBindings)
        {
            var mergeResult = MergeListHelper.MergeList(
                existingBindings,
                generatedBindings,
                (current, generated) => string.Equals(
                    current.LocalBlendshape,
                    generated.LocalBlendshape,
                    StringComparison.Ordinal),
                BindingsMatch,
                (current, generated) => CreateConflict(target, current, generated),
                conflictSession,
                generated => string.IsNullOrEmpty(generated.Blendshape));
            mergedBindings = mergeResult.Items;
            return mergeResult.Status;
        }

        private static bool BindingsMatch(BlendshapeBinding first, BlendshapeBinding second)
        {
            return AvatarObjectReferenceComparer.AreEqual(first.ReferenceMesh, second.ReferenceMesh) &&
                   string.Equals(first.Blendshape, second.Blendshape, StringComparison.Ordinal);
        }

        private static ConflictInfo CreateConflict(
            GameObject target,
            BlendshapeBinding existing,
            BlendshapeBinding generated)
        {
            return new ConflictInfo(
                $"{target.name} / {generated.LocalBlendshape}",
                FormatBinding(existing),
                FormatBinding(generated));
        }

        private static string FormatBinding(BlendshapeBinding binding)
        {
            if (string.IsNullOrEmpty(binding.Blendshape)) return "<none> (delete)";
            var referencePath = binding.ReferenceMesh?.referencePath;
            if (string.IsNullOrEmpty(referencePath)) referencePath = "<reference>";
            return $"{referencePath} / {binding.Blendshape}";
        }

        private static void ApplyPlans(
            IReadOnlyList<UpdatePlan> plans,
            BlendshapeSyncBuildResult result)
        {
            if (plans.Count == 0) return;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update MA Blendshape Sync");
            foreach (var plan in plans)
            {
                if (plan.Existing == null)
                {
                    var component = Undo.AddComponent<ModularAvatarBlendshapeSync>(plan.Target);
                    component.Bindings = plan.Bindings;
                    EditorUtility.SetDirty(component);
                    result.AddedCount++;
                }
                else
                {
                    Undo.RecordObject(plan.Existing, "Update MA Blendshape Sync");
                    plan.Existing.Bindings = plan.Bindings;
                    EditorUtility.SetDirty(plan.Existing);
                    result.UpdatedCount++;
                }
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
