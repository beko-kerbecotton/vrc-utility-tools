using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class MaterialSwapToolsTests
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
        public void Collect_ReturnsUniqueMaterialsIncludingInactiveRenderers()
        {
            var root = CreateGameObject("root");
            var first = CreateMaterial();
            var second = CreateMaterial();
            CreateRenderer(root.transform, "active", first, null, second);
            var inactive = CreateRenderer(root.transform, "inactive", first);
            inactive.gameObject.SetActive(false);

            var entries = MaterialSwapCollector.Collect(root);

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Original, Is.SameAs(first));
            Assert.That(entries[1].Original, Is.SameAs(second));
        }

        [Test]
        public void Build_SkipsEntriesWithoutReplacement()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var first = CreateMaterial();
            var second = CreateMaterial();
            var replacement = CreateMaterial();
            var entries = new[]
            {
                new MaterialSwapEntry(first) { Replacement = replacement },
                new MaterialSwapEntry(second)
            };

            var status = MaterialSwapBuilder.Build(componentTarget, targetRoot, entries);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Success));
            var component = componentTarget.GetComponent<ModularAvatarMaterialSwap>();
            Assert.That(component, Is.Not.Null);
            Assert.That(component.Swaps, Has.Count.EqualTo(1));
            Assert.That(component.Swaps[0].From, Is.SameAs(first));
            Assert.That(component.Swaps[0].To, Is.SameAs(replacement));
            Assert.That(component.QuickSwapMode, Is.EqualTo(QuickSwapMode.None));
        }

        [Test]
        public void Build_WithOnlyEnabledNone_DoesNotAddComponent()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var original = CreateMaterial();

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                new[] { new MaterialSwapEntry(original) });

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.NoAssignments));
            Assert.That(componentTarget.GetComponent<ModularAvatarMaterialSwap>(), Is.Null);
        }

        [Test]
        public void Build_WithEnabledAndDisabledEntries_GeneratesOnlyEnabledEntry()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var enabledOriginal = CreateMaterial();
            var enabledReplacement = CreateMaterial();
            var disabledOriginal = CreateMaterial();
            var disabledReplacement = CreateMaterial();
            var disabledEntry = new MaterialSwapEntry(disabledOriginal)
            {
                Replacement = disabledReplacement,
                Enabled = false
            };
            var entries = new[]
            {
                new MaterialSwapEntry(enabledOriginal) { Replacement = enabledReplacement },
                disabledEntry
            };

            var status = MaterialSwapBuilder.Build(componentTarget, targetRoot, entries);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Success));
            var component = componentTarget.GetComponent<ModularAvatarMaterialSwap>();
            Assert.That(component.Swaps, Has.Count.EqualTo(1));
            Assert.That(component.Swaps[0].From, Is.SameAs(enabledOriginal));
            Assert.That(component.Swaps[0].To, Is.SameAs(enabledReplacement));
            Assert.That(disabledEntry.Replacement, Is.SameAs(disabledReplacement));
        }

        [Test]
        public void Build_WithNoAssignments_DoesNotAddComponent()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var entries = new[] { new MaterialSwapEntry(CreateMaterial()) };

            var status = MaterialSwapBuilder.Build(componentTarget, targetRoot, entries);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.NoAssignments));
            Assert.That(componentTarget.GetComponent<ModularAvatarMaterialSwap>(), Is.Null);
        }

        [Test]
        public void Build_WithDirectoryMode_StoresQuickSwapMode()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var entry = new MaterialSwapEntry(CreateMaterial()) { Replacement = CreateMaterial() };

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                new[] { entry },
                QuickSwapMode.SiblingDirectory);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Success));
            Assert.That(
                componentTarget.GetComponent<ModularAvatarMaterialSwap>().QuickSwapMode,
                Is.EqualTo(QuickSwapMode.SiblingDirectory));
        }

        [Test]
        public void Build_WithExistingComponent_DoesNotModifyIt()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            var originalSwaps = existing.Swaps;
            var entries = new[]
            {
                new MaterialSwapEntry(CreateMaterial()) { Replacement = CreateMaterial() }
            };

            var status = MaterialSwapBuilder.Build(componentTarget, targetRoot, entries);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.AlreadyExists));
            Assert.That(componentTarget.GetComponents<ModularAvatarMaterialSwap>(), Has.Length.EqualTo(1));
            Assert.That(existing.Swaps, Is.SameAs(originalSwaps));
        }

        [Test]
        public void Build_Merge_AddsNonConflictingSwap()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var existingFrom = CreateMaterial();
            var generatedFrom = CreateMaterial();
            var replacement = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(targetRoot);
            existing.QuickSwapMode = QuickSwapMode.None;
            existing.Swaps = new List<MatSwap>
            {
                CreateSwap(existingFrom, replacement)
            };
            var entries = new[]
            {
                new MaterialSwapEntry(generatedFrom) { Replacement = replacement }
            };

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                entries,
                QuickSwapMode.None,
                ExistingComponentHandlingMode.Merge);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(existing.Swaps, Has.Count.EqualTo(2));
            Assert.That(existing.Swaps[1].From, Is.SameAs(generatedFrom));
        }

        [Test]
        public void Build_MergeApplyToAll_OverwritesRootModeAndSwapWithOneResolution()
        {
            var componentTarget = CreateGameObject("component target");
            var existingRoot = CreateGameObject("existing root");
            var generatedRoot = CreateGameObject("generated root");
            var from = CreateMaterial();
            var existingTo = CreateMaterial();
            var generatedTo = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(existingRoot);
            existing.QuickSwapMode = QuickSwapMode.None;
            existing.Swaps = new List<MatSwap> { CreateSwap(from, existingTo) };
            var entries = new[]
            {
                new MaterialSwapEntry(from) { Replacement = generatedTo }
            };
            var resolutionCount = 0;
            var session = new ConflictResolutionSession(_ =>
            {
                resolutionCount++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, true);
            });

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                generatedRoot,
                entries,
                QuickSwapMode.SiblingDirectory,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(resolutionCount, Is.EqualTo(1));
            Assert.That(existing.Root, Is.EqualTo(new AvatarObjectReference(generatedRoot)));
            Assert.That(existing.QuickSwapMode, Is.EqualTo(QuickSwapMode.SiblingDirectory));
            Assert.That(existing.Swaps[0].To, Is.SameAs(generatedTo));
        }

        [Test]
        public void Build_MergeCancelled_DoesNotApplyEarlierNonConflict()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var existingFrom = CreateMaterial();
            var newFrom = CreateMaterial();
            var existingTo = CreateMaterial();
            var generatedTo = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(targetRoot);
            existing.QuickSwapMode = QuickSwapMode.None;
            existing.Swaps = new List<MatSwap> { CreateSwap(existingFrom, existingTo) };
            var originalRoot = existing.Root;
            var originalSwaps = existing.Swaps;
            var entries = new[]
            {
                new MaterialSwapEntry(newFrom) { Replacement = generatedTo },
                new MaterialSwapEntry(existingFrom) { Replacement = generatedTo }
            };
            var session = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Cancel, false));

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                entries,
                QuickSwapMode.None,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Cancelled));
            Assert.That(existing.Root, Is.SameAs(originalRoot));
            Assert.That(existing.Swaps, Is.SameAs(originalSwaps));
            Assert.That(existing.Swaps, Has.Count.EqualTo(1));
            Assert.That(existing.Swaps[0].To, Is.SameAs(existingTo));
        }

        [Test]
        public void Build_Overwrite_ReplacesContentsWithoutReplacingComponent()
        {
            var componentTarget = CreateGameObject("component target");
            var existingRoot = CreateGameObject("existing root");
            var generatedRoot = CreateGameObject("generated root");
            var from = CreateMaterial();
            var replacement = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(existingRoot);
            existing.QuickSwapMode = QuickSwapMode.None;
            existing.Swaps = new List<MatSwap>();
            var entries = new[]
            {
                new MaterialSwapEntry(from) { Replacement = replacement }
            };

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                generatedRoot,
                entries,
                QuickSwapMode.SameDirectory,
                ExistingComponentHandlingMode.Overwrite);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(componentTarget.GetComponents<ModularAvatarMaterialSwap>(), Has.Length.EqualTo(1));
            Assert.That(componentTarget.GetComponent<ModularAvatarMaterialSwap>(), Is.SameAs(existing));
            Assert.That(existing.Root, Is.EqualTo(new AvatarObjectReference(generatedRoot)));
            Assert.That(existing.QuickSwapMode, Is.EqualTo(QuickSwapMode.SameDirectory));
            Assert.That(existing.Swaps, Has.Count.EqualTo(1));
            Assert.That(existing.Swaps[0].From, Is.SameAs(from));
        }

        [Test]
        public void Build_MergeMatchingContents_DoesNotUpdateOrDuplicate()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var from = CreateMaterial();
            var to = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(targetRoot);
            existing.QuickSwapMode = QuickSwapMode.None;
            existing.Swaps = new List<MatSwap> { CreateSwap(from, to) };
            var originalSwaps = existing.Swaps;
            var entries = new[]
            {
                new MaterialSwapEntry(from) { Replacement = to }
            };

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                entries,
                QuickSwapMode.None,
                ExistingComponentHandlingMode.Merge);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.NoChanges));
            Assert.That(existing.Swaps, Is.SameAs(originalSwaps));
            Assert.That(existing.Swaps, Has.Count.EqualTo(1));
        }

        [Test]
        public void Build_MergeEnabledNoneDeletesOnlyOverwrittenKey()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var keptFrom = CreateMaterial();
            var deletedFrom = CreateMaterial();
            var to = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(targetRoot);
            existing.QuickSwapMode = QuickSwapMode.None;
            existing.Swaps = new List<MatSwap>
            {
                CreateSwap(keptFrom, to),
                CreateSwap(deletedFrom, to)
            };
            ConflictInfo conflict = null;
            var session = new ConflictResolutionSession(info =>
            {
                conflict = info;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, false);
            });

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                new[] { new MaterialSwapEntry(deletedFrom) },
                QuickSwapMode.None,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(existing.Swaps, Has.Count.EqualTo(1));
            Assert.That(existing.Swaps[0].From, Is.SameAs(keptFrom));
            Assert.That(conflict.GeneratedValue, Is.EqualTo("<none> (delete)"));
        }

        [Test]
        public void Build_OverwriteEnabledNoneClearsExistingSwaps()
        {
            var componentTarget = CreateGameObject("component target");
            var targetRoot = CreateGameObject("renderer root");
            var from = CreateMaterial();
            var to = CreateMaterial();
            var existing = componentTarget.AddComponent<ModularAvatarMaterialSwap>();
            existing.Root = new AvatarObjectReference(targetRoot);
            existing.Swaps = new List<MatSwap> { CreateSwap(from, to) };

            var status = MaterialSwapBuilder.Build(
                componentTarget,
                targetRoot,
                new[] { new MaterialSwapEntry(from) },
                QuickSwapMode.None,
                ExistingComponentHandlingMode.Overwrite);

            Assert.That(status, Is.EqualTo(ComponentBuildStatus.Updated));
            Assert.That(existing.Swaps, Is.Empty);
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

        private static MatSwap CreateSwap(Material from, Material to)
        {
            return new MatSwap { From = from, To = to };
        }
    }
}
