using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class BlendshapeToolsTests
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
        public void CollectModel_GroupsSameNameAcrossRenderers()
        {
            var reference = CreateRenderer(null, "reference", "Smile", "Blink");
            var root = CreateGameObject("root");
            CreateRenderer(root.transform, "first", "Smile", "Blink");
            CreateRenderer(root.transform, "second", "Smile");

            var model = BlendshapeCollector.CollectModel(reference, root);

            Assert.That(model.LocalBlendshapes, Is.EqualTo(new[] { "Blink", "Smile" }));
            Assert.That(model.GetBulkState("Smile").RendererCount, Is.EqualTo(2));
            Assert.That(model.Renderers[0].Mappings.ContainsKey("Smile"), Is.True);
            Assert.That(model.Renderers[1].Mappings.ContainsKey("Smile"), Is.True);
        }

        [Test]
        public void Build_SkipsExistingComponentWithoutChangingBindings()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var target = CreateRenderer(null, "target", "Smile");
            var existing = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings.Add(new BlendshapeBinding { Blendshape = "Existing" });
            var originalBindings = existing.Bindings;
            var model = CreateModel(reference, target);

            var result = BlendshapeSyncBuilder.Build(model);

            Assert.That(result.AddedCount, Is.Zero);
            Assert.That(result.ExistingComponentTargets, Has.Count.EqualTo(1));
            Assert.That(target.GetComponents<ModularAvatarBlendshapeSync>(), Has.Length.EqualTo(1));
            Assert.That(existing.Bindings, Is.SameAs(originalBindings));
            Assert.That(existing.Bindings[0].Blendshape, Is.EqualTo("Existing"));
        }

        [Test]
        public void CollectModel_DiscoversInactiveRenderersAndInitializesNameMatches()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var root = CreateGameObject("root");
            var first = CreateRenderer(root.transform, "first", "Smile", "Blink");
            var second = CreateRenderer(root.transform, "second", "Smile");
            second.gameObject.SetActive(false);

            var model = BlendshapeCollector.CollectModel(reference, root);

            Assert.That(model.Renderers, Has.Count.EqualTo(2));
            Assert.That(model.Renderers[0].Renderer, Is.SameAs(first));
            Assert.That(model.Renderers[1].Renderer, Is.SameAs(second));
            Assert.That(model.Renderers[0].Mappings["Smile"].Enabled, Is.True);
            Assert.That(model.Renderers[0].Mappings["Smile"].ReferenceBlendshape, Is.EqualTo("Smile"));
            Assert.That(model.Renderers[0].Mappings["Blink"].Enabled, Is.False);
        }

        [Test]
        public void CollectModel_ExcludesRendererWithoutBlendshapes()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var root = CreateGameObject("root");
            var withBlendshape = CreateRenderer(root.transform, "with", "Smile");
            CreateRenderer(root.transform, "without");

            var model = BlendshapeCollector.CollectModel(reference, root);

            Assert.That(model.Renderers, Has.Count.EqualTo(1));
            Assert.That(model.Renderers[0].Renderer, Is.SameAs(withBlendshape));
        }

        [Test]
        public void BulkState_DetectsMixedValuesAndBulkSetOverwritesIncludedRenderers()
        {
            var reference = CreateRenderer(null, "reference", "Smile", "Happy");
            var first = CreateRenderer(null, "first", "Smile");
            var second = CreateRenderer(null, "second", "Smile");
            var model = CreateModel(reference, first, second);
            model.Renderers[1].Mappings["Smile"].Enabled = false;
            model.Renderers[1].Mappings["Smile"].ReferenceBlendshape = "Happy";

            var mixed = model.GetBulkState("Smile");
            Assert.That(mixed.EnabledIsMixed, Is.True);
            Assert.That(mixed.ReferenceIsMixed, Is.True);

            model.SetBulkEnabled("Smile", true);
            model.SetBulkReference("Smile", "Happy");

            var unified = model.GetBulkState("Smile");
            Assert.That(unified.EnabledIsMixed, Is.False);
            Assert.That(unified.Enabled, Is.True);
            Assert.That(unified.ReferenceIsMixed, Is.False);
            Assert.That(unified.ReferenceBlendshape, Is.EqualTo("Happy"));
        }

        [Test]
        public void BulkSet_DoesNotChangeExcludedRenderer()
        {
            var reference = CreateRenderer(null, "reference", "Smile", "Happy");
            var included = CreateRenderer(null, "included", "Smile");
            var excluded = CreateRenderer(null, "excluded", "Smile");
            var model = CreateModel(reference, included, excluded);
            model.Renderers[1].Included = false;

            model.SetBulkReference("Smile", "Happy");

            Assert.That(model.Renderers[0].Mappings["Smile"].ReferenceBlendshape, Is.EqualTo("Happy"));
            Assert.That(model.Renderers[1].Mappings["Smile"].ReferenceBlendshape, Is.EqualTo("Smile"));
        }

        [Test]
        public void Build_DoesNotAddComponentToExcludedRenderer()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var included = CreateRenderer(null, "included", "Smile");
            var excluded = CreateRenderer(null, "excluded", "Smile");
            var model = CreateModel(reference, included, excluded);
            model.Renderers[1].Included = false;

            var result = BlendshapeSyncBuilder.Build(model);

            Assert.That(result.AddedCount, Is.EqualTo(1));
            Assert.That(included.GetComponent<ModularAvatarBlendshapeSync>(), Is.Not.Null);
            Assert.That(excluded.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
        }

        [Test]
        public void Build_Merge_AddsNonConflictingBinding()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var target = CreateRenderer(null, "target", "Smile");
            var existing = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Blink", "Blink")
            };
            var model = CreateModel(reference, target);

            var result = BlendshapeSyncBuilder.Build(
                model,
                ExistingComponentHandlingMode.Merge);

            Assert.That(result.UpdatedCount, Is.EqualTo(1));
            Assert.That(existing.Bindings, Has.Count.EqualTo(2));
            Assert.That(existing.Bindings[1].LocalBlendshape, Is.EqualTo("Smile"));
        }

        [Test]
        public void Build_MergeApplyToAll_OverwritesConflictsAcrossRenderersOnce()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var first = CreateRenderer(null, "first", "Smile");
            var second = CreateRenderer(null, "second", "Smile");
            var firstComponent = first.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            var secondComponent = second.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            firstComponent.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Existing", "Smile")
            };
            secondComponent.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Existing", "Smile")
            };
            var model = CreateModel(reference, first, second);
            var resolutionCount = 0;
            var session = new ConflictResolutionSession(_ =>
            {
                resolutionCount++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, true);
            });

            var result = BlendshapeSyncBuilder.Build(
                model,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(result.UpdatedCount, Is.EqualTo(2));
            Assert.That(resolutionCount, Is.EqualTo(1));
            Assert.That(firstComponent.Bindings[0].Blendshape, Is.EqualTo("Smile"));
            Assert.That(secondComponent.Bindings[0].Blendshape, Is.EqualTo("Smile"));
        }

        [Test]
        public void Build_MergeCancelled_DoesNotApplyAnyRendererPlan()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var newTarget = CreateRenderer(null, "new", "Smile");
            var conflictTarget = CreateRenderer(null, "conflict", "Smile");
            var existing = conflictTarget.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Existing", "Smile")
            };
            var originalBindings = existing.Bindings;
            var model = CreateModel(reference, newTarget, conflictTarget);
            var session = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Cancel, false));

            var result = BlendshapeSyncBuilder.Build(
                model,
                ExistingComponentHandlingMode.Merge,
                session);

            Assert.That(result.Cancelled, Is.True);
            Assert.That(newTarget.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            Assert.That(existing.Bindings, Is.SameAs(originalBindings));
            Assert.That(existing.Bindings[0].Blendshape, Is.EqualTo("Existing"));
        }

        [Test]
        public void Build_Overwrite_ReplacesBindingsWithoutReplacingComponent()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var target = CreateRenderer(null, "target", "Smile");
            var existing = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Existing", "Other")
            };
            var model = CreateModel(reference, target);

            var result = BlendshapeSyncBuilder.Build(
                model,
                ExistingComponentHandlingMode.Overwrite);

            Assert.That(result.UpdatedCount, Is.EqualTo(1));
            Assert.That(target.GetComponents<ModularAvatarBlendshapeSync>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponent<ModularAvatarBlendshapeSync>(), Is.SameAs(existing));
            Assert.That(existing.Bindings, Has.Count.EqualTo(1));
            Assert.That(existing.Bindings[0].LocalBlendshape, Is.EqualTo("Smile"));
            Assert.That(existing.Bindings[0].Blendshape, Is.EqualTo("Smile"));
        }

        [Test]
        public void Build_MergeMatchingBinding_DoesNotUpdateOrDuplicate()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var target = CreateRenderer(null, "target", "Smile");
            var existing = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Smile", "Smile")
            };
            var originalBindings = existing.Bindings;
            var model = CreateModel(reference, target);

            var result = BlendshapeSyncBuilder.Build(
                model,
                ExistingComponentHandlingMode.Merge);

            Assert.That(result.UnchangedCount, Is.EqualTo(1));
            Assert.That(result.UpdatedCount, Is.Zero);
            Assert.That(existing.Bindings, Is.SameAs(originalBindings));
            Assert.That(existing.Bindings, Has.Count.EqualTo(1));
        }

        [Test]
        public void Build_MergeEnabledNoneDeletesOnlyOverwrittenKey()
        {
            var reference = CreateRenderer(null, "reference", "Smile", "Blink");
            var target = CreateRenderer(null, "target", "Smile", "Blink");
            var existing = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Smile", "Smile"),
                CreateBinding(reference, "Blink", "Blink")
            };
            var model = CreateModel(reference, target);
            model.Renderers[0].Mappings["Smile"].Enabled = false;
            model.Renderers[0].Mappings["Blink"].ReferenceBlendshape = null;
            ConflictInfo conflict = null;
            var session = new ConflictResolutionSession(info =>
            {
                conflict = info;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, false);
            });

            var result = BlendshapeSyncBuilder.Build(
                model, ExistingComponentHandlingMode.Merge, session);

            Assert.That(result.UpdatedCount, Is.EqualTo(1));
            Assert.That(existing.Bindings, Has.Count.EqualTo(1));
            Assert.That(existing.Bindings[0].LocalBlendshape, Is.EqualTo("Smile"));
            Assert.That(conflict.GeneratedValue, Is.EqualTo("<none> (delete)"));
        }

        [Test]
        public void Build_OverwriteEnabledNoneClearsExistingBindings()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var target = CreateRenderer(null, "target", "Smile");
            var existing = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            existing.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Smile", "Smile")
            };
            var model = CreateModel(reference, target);
            model.Renderers[0].Mappings["Smile"].ReferenceBlendshape = null;

            var result = BlendshapeSyncBuilder.Build(
                model, ExistingComponentHandlingMode.Overwrite);

            Assert.That(result.UpdatedCount, Is.EqualTo(1));
            Assert.That(existing.Bindings, Is.Empty);
        }

        [Test]
        public void Build_DeletionDoesNotTouchExcludedRendererOrCreateComponent()
        {
            var reference = CreateRenderer(null, "reference", "Smile");
            var included = CreateRenderer(null, "included", "Smile");
            var excluded = CreateRenderer(null, "excluded", "Smile");
            var excludedComponent = excluded.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            excludedComponent.Bindings = new List<BlendshapeBinding>
            {
                CreateBinding(reference, "Smile", "Smile")
            };
            var originalBindings = excludedComponent.Bindings;
            var model = CreateModel(reference, included, excluded);
            model.Renderers[0].Mappings["Smile"].ReferenceBlendshape = null;
            model.Renderers[1].Included = false;

            var result = BlendshapeSyncBuilder.Build(
                model, ExistingComponentHandlingMode.Merge,
                new ConflictResolutionSession(_ =>
                    new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, false)));

            Assert.That(result.AddedCount, Is.Zero);
            Assert.That(included.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            Assert.That(excludedComponent.Bindings, Is.SameAs(originalBindings));
        }

        private static BlendshapeMappingModel CreateModel(
            SkinnedMeshRenderer reference,
            params SkinnedMeshRenderer[] renderers)
        {
            var entries = new List<BlendshapeRendererEntry>();
            foreach (var renderer in renderers)
            {
                var mappings = new List<RendererBlendshapeMapping>();
                foreach (var name in BlendshapeCollector.GetBlendshapeNames(renderer))
                {
                    mappings.Add(new RendererBlendshapeMapping(name, name, true));
                }
                entries.Add(new BlendshapeRendererEntry(renderer, mappings));
            }

            return new BlendshapeMappingModel(
                reference,
                entries,
                BlendshapeCollector.GetBlendshapeNames(reference));
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static BlendshapeBinding CreateBinding(
            SkinnedMeshRenderer reference,
            string referenceBlendshape,
            string localBlendshape)
        {
            return new BlendshapeBinding
            {
                ReferenceMesh = new AvatarObjectReference(reference.gameObject),
                Blendshape = referenceBlendshape,
                LocalBlendshape = localBlendshape
            };
        }

        private SkinnedMeshRenderer CreateRenderer(Transform parent, string name, params string[] blendshapes)
        {
            var gameObject = CreateGameObject(name);
            if (parent != null) gameObject.transform.SetParent(parent);
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = name + "Mesh" };
            mesh.vertices = new[] { Vector3.zero };
            foreach (var blendshape in blendshapes)
            {
                mesh.AddBlendShapeFrame(blendshape, 100f, new[] { Vector3.zero }, null, null);
            }
            renderer.sharedMesh = mesh;
            _objects.Add(mesh);
            return renderer;
        }
    }
}
