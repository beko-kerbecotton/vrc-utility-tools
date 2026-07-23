using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class AvatarObjectReferenceComparerTests
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
        public void ReferencesToSameGameObject_AreEqual()
        {
            var target = CreateGameObject("target");
            var first = new AvatarObjectReference(target);
            var second = new AvatarObjectReference(target);

            Assert.That(AvatarObjectReferenceComparer.AreEqual(first, second), Is.True);
        }

        [Test]
        public void ReferencesToDifferentGameObjects_AreNotEqual()
        {
            var firstTarget = CreateGameObject("first");
            var secondTarget = CreateGameObject("second");
            var first = new AvatarObjectReference(firstTarget);
            var second = new AvatarObjectReference(secondTarget);

            Assert.That(AvatarObjectReferenceComparer.AreEqual(first, second), Is.False);
        }

        [Test]
        public void TwoMissingReferences_AreEqual()
        {
            Assert.That(AvatarObjectReferenceComparer.AreEqual(null, null), Is.True);
        }

        [Test]
        public void MissingAndExistingReferences_AreNotEqual()
        {
            var target = CreateGameObject("target");
            var reference = new AvatarObjectReference(target);

            Assert.That(AvatarObjectReferenceComparer.AreEqual(null, reference), Is.False);
            Assert.That(AvatarObjectReferenceComparer.AreEqual(reference, null), Is.False);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }
    }
}
