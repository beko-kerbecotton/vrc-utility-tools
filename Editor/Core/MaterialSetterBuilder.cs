using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public static class MaterialSetterBuilder
    {
        public static ComponentBuildStatus Build(
            GameObject componentTarget,
            MaterialMappingModel model,
            ExistingComponentHandlingMode handlingMode = ExistingComponentHandlingMode.Skip,
            ConflictResolutionSession conflictSession = null)
        {
            if (componentTarget == null || model == null) return ComponentBuildStatus.InvalidTarget;

            var commands = new List<MaterialSwitchObject>();
            foreach (var renderer in model.Renderers)
            {
                if (!renderer.Included || renderer.Renderer == null) continue;
                foreach (var slot in renderer.Slots)
                {
                    if (!slot.Enabled) continue;
                    commands.Add(new MaterialSwitchObject
                    {
                        Object = new AvatarObjectReference(renderer.Renderer.gameObject),
                        Material = slot.Replacement,
                        MaterialIndex = slot.MaterialIndex
                    });
                }
            }

            return Apply(componentTarget, commands, handlingMode, conflictSession);
        }

        private static ComponentBuildStatus Apply(
            GameObject componentTarget,
            List<MaterialSwitchObject> generatedCommands,
            ExistingComponentHandlingMode handlingMode,
            ConflictResolutionSession conflictSession)
        {
            var existing = componentTarget.GetComponent<ModularAvatarMaterialSetter>();
            if (existing != null && handlingMode == ExistingComponentHandlingMode.Skip)
                return ComponentBuildStatus.AlreadyExists;

            var generatedObjects = generatedCommands.FindAll(item => item.Material != null);
            if (existing == null)
            {
                if (generatedObjects.Count == 0) return ComponentBuildStatus.NoAssignments;
                var component = Undo.AddComponent<ModularAvatarMaterialSetter>(componentTarget);
                component.Objects = generatedObjects;
                EditorUtility.SetDirty(component);
                return ComponentBuildStatus.Success;
            }

            if (handlingMode == ExistingComponentHandlingMode.Overwrite)
                return UpdateExisting(existing, generatedObjects);

            var mergeResult = MergeListHelper.MergeList(
                existing.Objects,
                generatedCommands,
                KeysMatch,
                (current, generated) => current.Material == generated.Material,
                (current, generated) => CreateConflict(generated, current.Material),
                conflictSession,
                item => item.Material == null);
            if (mergeResult.Status == MergeStatus.Cancelled)
                return ComponentBuildStatus.Cancelled;

            return mergeResult.Status == MergeStatus.Changed
                ? UpdateExisting(existing, mergeResult.Items)
                : ComponentBuildStatus.NoChanges;
        }

        private static ComponentBuildStatus UpdateExisting(
            ModularAvatarMaterialSetter component,
            List<MaterialSwitchObject> objects)
        {
            Undo.RecordObject(component, "Update MA Material Setter");
            component.Objects = objects;
            EditorUtility.SetDirty(component);
            return ComponentBuildStatus.Updated;
        }

        private static bool KeysMatch(
            MaterialSwitchObject candidate,
            MaterialSwitchObject target)
        {
            return candidate != null &&
                   AvatarObjectReferenceComparer.AreEqual(candidate.Object, target.Object) &&
                   candidate.MaterialIndex == target.MaterialIndex;
        }

        private static ConflictInfo CreateConflict(
            MaterialSwitchObject generated,
            Material existingMaterial)
        {
            var objectPath = generated.Object?.referencePath;
            if (string.IsNullOrEmpty(objectPath)) objectPath = "<object>";
            return new ConflictInfo(
                $"{objectPath} [{generated.MaterialIndex}]",
                existingMaterial != null ? existingMaterial.name : "<none>",
                generated.Material != null ? generated.Material.name : "<none> (delete)");
        }
    }
}
