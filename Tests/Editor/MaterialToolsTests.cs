using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class MaterialToolsTests
    {
        private readonly List<Object> _objects = new List<Object>();

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
        public void CollectModel_GroupsSharedMaterialAndSlots()
        {
            var root = CreateGameObject("root");
            var material = CreateMaterial();
            CreateRenderer(root.transform, "first", material, material);
            CreateRenderer(root.transform, "second", material);

            var model = MaterialCollector.CollectModel(root);

            Assert.That(model.Renderers, Has.Count.EqualTo(2));
            Assert.That(model.OriginalMaterials, Is.EqualTo(new[] { material }));
            Assert.That(model.GetBulkState(material).SlotCount, Is.EqualTo(3));
        }

        [Test]
        public void Build_WithExistingComponent_DoesNotModifyIt()
        {
            var target = CreateGameObject("target");
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            var originalObjects = existing.Objects;

            var status = MaterialSetterBuilder.Build(
                target,
                new MaterialMappingModel(new List<MaterialRendererEntry>()));

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.AlreadyExists));
            Assert.That(target.GetComponents<ModularAvatarMaterialSetter>(), Has.Length.EqualTo(1));
            Assert.That(existing.Objects, Is.SameAs(originalObjects));
        }

        [Test]
        public void Build_WithNoReplacement_DoesNotAddComponent()
        {
            var target = CreateGameObject("target");
            var sourceRoot = CreateGameObject("source");
            var material = CreateMaterial();
            CreateRenderer(sourceRoot.transform, "renderer", material);
            var model = MaterialCollector.CollectModel(sourceRoot);

            var status = MaterialSetterBuilder.Build(target, model);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.NoAssignments));
            Assert.That(target.GetComponent<ModularAvatarMaterialSetter>(), Is.Null);
        }

        [Test]
        public void Build_AfterDisablingBulkMaterial_ExcludesDisabledMaterialSlots()
        {
            var target = CreateGameObject("target");
            var sourceRoot = CreateGameObject("source");
            var firstMaterial = CreateMaterial();
            var secondMaterial = CreateMaterial();
            var replacementMaterial = CreateMaterial();
            CreateRenderer(sourceRoot.transform, "renderer", firstMaterial, secondMaterial);

            var model = MaterialCollector.CollectModel(sourceRoot);
            model.SetBulkReplacement(firstMaterial, replacementMaterial);
            model.SetBulkReplacement(secondMaterial, replacementMaterial);
            model.SetBulkEnabled(firstMaterial, false);

            var status = MaterialSetterBuilder.Build(target, model);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Success));
            var component = target.GetComponent<ModularAvatarMaterialSetter>();
            Assert.That(component.Objects, Has.Count.EqualTo(1));
            Assert.That(component.Objects[0].MaterialIndex, Is.EqualTo(1));
        }

        [Test]
        public void CollectModel_DiscoversInactiveRenderersAndSlots()
        {
            var root = CreateGameObject("root");
            var material = CreateMaterial();
            var first = CreateRenderer(root.transform, "first", material, material);
            var second = CreateRenderer(root.transform, "second", material);
            second.gameObject.SetActive(false);

            var model = MaterialCollector.CollectModel(root);

            Assert.That(model.Renderers, Has.Count.EqualTo(2));
            Assert.That(model.Renderers[0].Renderer, Is.SameAs(first));
            Assert.That(model.Renderers[0].Slots, Has.Count.EqualTo(2));
            Assert.That(model.Renderers[1].Renderer, Is.SameAs(second));
        }

        [Test]
        public void BulkState_DetectsMixedReplacementAndOverwritesIncludedSlots()
        {
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var firstReplacement = CreateMaterial();
            var secondReplacement = CreateMaterial();
            CreateRenderer(root.transform, "first", original);
            CreateRenderer(root.transform, "second", original);
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[0].Slots[0].Replacement = firstReplacement;
            model.Renderers[1].Slots[0].Replacement = secondReplacement;

            Assert.That(model.GetBulkState(original).ReplacementIsMixed, Is.True);

            model.SetBulkReplacement(original, firstReplacement);

            var state = model.GetBulkState(original);
            Assert.That(state.ReplacementIsMixed, Is.False);
            Assert.That(state.Replacement, Is.SameAs(firstReplacement));
        }

        [Test]
        public void BulkSet_DoesNotChangeExcludedRenderer()
        {
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var replacement = CreateMaterial();
            CreateRenderer(root.transform, "included", original);
            CreateRenderer(root.transform, "excluded", original);
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[1].Included = false;

            model.SetBulkReplacement(original, replacement);

            Assert.That(model.Renderers[0].Slots[0].Replacement, Is.SameAs(replacement));
            Assert.That(model.Renderers[1].Slots[0].Replacement, Is.Null);
        }

        [Test]
        public void Build_FromModel_ExcludesDisabledSlotsAndRenderers()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var replacement = CreateMaterial();
            CreateRenderer(root.transform, "included", original, original);
            CreateRenderer(root.transform, "excluded", original);
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[0].Slots[0].Replacement = replacement;
            model.Renderers[0].Slots[1].Replacement = replacement;
            model.Renderers[0].Slots[1].Enabled = false;
            model.Renderers[1].Slots[0].Replacement = replacement;
            model.Renderers[1].Included = false;

            var status = MaterialSetterBuilder.Build(target, model);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Success));
            var component = target.GetComponent<ModularAvatarMaterialSetter>();
            Assert.That(component.Objects, Has.Count.EqualTo(1));
            Assert.That(component.Objects[0].MaterialIndex, Is.Zero);
        }

        [Test]
        public void Build_Merge_AddsNonConflictingSlots()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var replacement = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original, original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, replacement, 0)
            };
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[0].Slots[0].Enabled = false;
            model.Renderers[0].Slots[1].Replacement = replacement;

            var status = MaterialSetterBuilder.Build(
                target,
                model,
                ExistingComponentHandlingMode.Merge);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(existing.Objects, Has.Count.EqualTo(2));
            Assert.That(existing.Objects[1].MaterialIndex, Is.EqualTo(1));
        }

        [Test]
        public void Build_MergeSkippedConflict_PreservesExistingValue()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var existingMaterial = CreateMaterial();
            var generatedMaterial = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, existingMaterial, 0)
            };
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[0].Slots[0].Replacement = generatedMaterial;
            var session = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Skip, false));

            var status = MaterialSetterBuilder.Build(
                target,
                model,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.NoChanges));
            Assert.That(existing.Objects[0].Material, Is.SameAs(existingMaterial));
        }

        [Test]
        public void Build_MergeApplyToAll_OverwritesAllConflictsWithOneResolution()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var existingMaterial = CreateMaterial();
            var generatedMaterial = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original, original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, existingMaterial, 0),
                CreateSwitchObject(renderer.gameObject, existingMaterial, 1)
            };
            var model = MaterialCollector.CollectModel(root);
            model.SetBulkReplacement(original, generatedMaterial);
            var resolutionCount = 0;
            var session = new ConflictResolutionSession(_ =>
            {
                resolutionCount++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, true);
            });

            var status = MaterialSetterBuilder.Build(
                target,
                model,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(resolutionCount, Is.EqualTo(1));
            Assert.That(existing.Objects[0].Material, Is.SameAs(generatedMaterial));
            Assert.That(existing.Objects[1].Material, Is.SameAs(generatedMaterial));
        }

        [Test]
        public void Build_MergeCancelled_DoesNotApplyEarlierNonConflict()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var existingMaterial = CreateMaterial();
            var generatedMaterial = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original, original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, existingMaterial, 1)
            };
            var originalObjects = existing.Objects;
            var model = MaterialCollector.CollectModel(root);
            model.SetBulkReplacement(original, generatedMaterial);
            var session = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Cancel, false));

            var status = MaterialSetterBuilder.Build(
                target,
                model,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Cancelled));
            Assert.That(existing.Objects, Is.SameAs(originalObjects));
            Assert.That(existing.Objects, Has.Count.EqualTo(1));
            Assert.That(existing.Objects[0].Material, Is.SameAs(existingMaterial));
        }

        [Test]
        public void Build_Overwrite_ReplacesObjectsWithoutReplacingComponent()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var existingMaterial = CreateMaterial();
            var generatedMaterial = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, existingMaterial, 3)
            };
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[0].Slots[0].Replacement = generatedMaterial;

            var status = MaterialSetterBuilder.Build(
                target,
                model,
                ExistingComponentHandlingMode.Overwrite);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(target.GetComponents<ModularAvatarMaterialSetter>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponent<ModularAvatarMaterialSetter>(), Is.SameAs(existing));
            Assert.That(existing.Objects, Has.Count.EqualTo(1));
            Assert.That(existing.Objects[0].MaterialIndex, Is.Zero);
            Assert.That(existing.Objects[0].Material, Is.SameAs(generatedMaterial));
        }

        [Test]
        public void Build_MergeEnabledNoneDeletesOnlyOverwrittenKey()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var assigned = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original, original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, assigned, 0),
                CreateSwitchObject(renderer.gameObject, assigned, 1)
            };
            var model = MaterialCollector.CollectModel(root);
            model.Renderers[0].Slots[0].Enabled = false;
            ConflictInfo conflict = null;
            var session = new ConflictResolutionSession(info =>
            {
                conflict = info;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, false);
            });

            var status = MaterialSetterBuilder.Build(
                target, model, ExistingComponentHandlingMode.Merge, session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(existing.Objects, Has.Count.EqualTo(1));
            Assert.That(existing.Objects[0].MaterialIndex, Is.Zero);
            Assert.That(conflict.GeneratedValue, Is.EqualTo("<none> (delete)"));
        }

        [Test]
        public void Build_OverwriteEnabledNoneClearsExistingObjects()
        {
            var target = CreateGameObject("target");
            var root = CreateGameObject("root");
            var original = CreateMaterial();
            var assigned = CreateMaterial();
            var renderer = CreateRenderer(root.transform, "renderer", original);
            var existing = target.AddComponent<ModularAvatarMaterialSetter>();
            existing.Objects = new List<MaterialSwitchObject>
            {
                CreateSwitchObject(renderer.gameObject, assigned, 0)
            };
            var model = MaterialCollector.CollectModel(root);

            var status = MaterialSetterBuilder.Build(
                target, model, ExistingComponentHandlingMode.Overwrite);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(existing.Objects, Is.Empty);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            _objects.Add(material);
            return material;
        }

        private MeshRenderer CreateRenderer(Transform parent, string name, params Material[] materials)
        {
            var gameObject = CreateGameObject(name);
            gameObject.transform.SetParent(parent);
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static MaterialSwitchObject CreateSwitchObject(
            GameObject target,
            Material material,
            int materialIndex)
        {
            return new MaterialSwitchObject
            {
                Object = new AvatarObjectReference(target),
                Material = material,
                MaterialIndex = materialIndex
            };
        }
    }
}
