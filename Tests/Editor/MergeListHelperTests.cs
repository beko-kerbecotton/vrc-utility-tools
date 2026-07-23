using System.Collections.Generic;
using net.bekobeko.utilitytools.core;
using NUnit.Framework;
using UnityEngine;

namespace net.bekobeko.utilitytools.tests
{
    public sealed class MergeListHelperTests
    {
        private sealed class Item
        {
            public string Key { get; }
            public string Value { get; }

            public Item(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }

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
        public void MergeList_NullSessionSkipsConflictAndAddsNonConflict()
        {
            var existing = new List<Item> { new Item("conflict", "existing") };
            var generated = new List<Item>
            {
                new Item("conflict", "generated"),
                new Item("new", "value")
            };

            var result = MergeListHelper.MergeList(
                existing,
                generated,
                KeysMatch,
                ValuesMatch,
                CreateConflict,
                null);

            Assert.That(result.Status, Is.EqualTo(MergeStatus.Changed));
            Assert.That(result.Items, Has.Count.EqualTo(2));
            Assert.That(result.Items[0], Is.SameAs(existing[0]));
            Assert.That(result.Items[1], Is.SameAs(generated[1]));
        }

        [Test]
        public void MergeList_ResolvesConflictsInGeneratedOrderAndHonorsApplyToAll()
        {
            var existing = new List<Item>
            {
                new Item("first", "existing"),
                new Item("second", "existing")
            };
            var generated = new List<Item>
            {
                new Item("first", "generated"),
                new Item("second", "generated")
            };
            var subjects = new List<string>();
            var resolutionCount = 0;
            var session = new ConflictResolutionSession(conflict =>
            {
                resolutionCount++;
                subjects.Add(conflict.Subject);
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, true);
            });

            var result = MergeListHelper.MergeList(
                existing,
                generated,
                KeysMatch,
                ValuesMatch,
                CreateConflict,
                session);

            Assert.That(result.Status, Is.EqualTo(MergeStatus.Changed));
            Assert.That(resolutionCount, Is.EqualTo(1));
            Assert.That(subjects, Is.EqualTo(new[] { "first" }));
            Assert.That(result.Items[0], Is.SameAs(generated[0]));
            Assert.That(result.Items[1], Is.SameAs(generated[1]));
        }

        [Test]
        public void MergeList_CancelReturnsWithoutChangingExistingList()
        {
            var original = new Item("conflict", "existing");
            var existing = new List<Item> { original };
            var generated = new List<Item>
            {
                new Item("new", "value"),
                new Item("conflict", "generated")
            };
            var session = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Cancel, false));

            var result = MergeListHelper.MergeList(
                existing,
                generated,
                KeysMatch,
                ValuesMatch,
                CreateConflict,
                session);

            Assert.That(result.Status, Is.EqualTo(MergeStatus.Cancelled));
            Assert.That(existing, Has.Count.EqualTo(1));
            Assert.That(existing[0], Is.SameAs(original));
        }

        [Test]
        public void MergeList_DeletionHonorsApplyToAllAndRemovesExistingKeys()
        {
            var existing = new List<Item>
            {
                new Item("first", "existing"),
                new Item("second", "existing"),
                new Item("kept", "existing")
            };
            var generated = new List<Item>
            {
                new Item("first", null),
                new Item("missing", null),
                new Item("second", null)
            };
            var resolutionCount = 0;
            var session = new ConflictResolutionSession(_ =>
            {
                resolutionCount++;
                return new ConflictResolutionDecision(ConflictResolutionAction.Overwrite, true);
            });

            var result = MergeListHelper.MergeList(
                existing,
                generated,
                KeysMatch,
                ValuesMatch,
                CreateConflict,
                session,
                item => item.Value == null);

            Assert.That(result.Status, Is.EqualTo(MergeStatus.Changed));
            Assert.That(resolutionCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Key, Is.EqualTo("kept"));
        }

        [Test]
        public void MergeList_DeletionSkipPreservesItemAndCancelDoesNotChangeSource()
        {
            var original = new Item("key", "existing");
            var existing = new List<Item> { original };
            var deletion = new List<Item> { new Item("key", null) };
            var skipSession = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Skip, false));

            var skipped = MergeListHelper.MergeList(
                existing, deletion, KeysMatch, ValuesMatch, CreateConflict, skipSession,
                item => item.Value == null);

            Assert.That(skipped.Status, Is.EqualTo(MergeStatus.Unchanged));
            Assert.That(skipped.Items[0], Is.SameAs(original));

            var cancelSession = new ConflictResolutionSession(_ =>
                new ConflictResolutionDecision(ConflictResolutionAction.Cancel, false));
            var cancelled = MergeListHelper.MergeList(
                existing, deletion, KeysMatch, ValuesMatch, CreateConflict, cancelSession,
                item => item.Value == null);

            Assert.That(cancelled.Status, Is.EqualTo(MergeStatus.Cancelled));
            Assert.That(existing, Has.Count.EqualTo(1));
            Assert.That(existing[0], Is.SameAs(original));
        }

        private static bool KeysMatch(Item existing, Item generated)
        {
            return existing.Key == generated.Key;
        }

        private static bool ValuesMatch(Item existing, Item generated)
        {
            return existing.Value == generated.Value;
        }

        private static ConflictInfo CreateConflict(Item existing, Item generated)
        {
            return new ConflictInfo(generated.Key, existing.Value, generated.Value);
        }
    }
}
