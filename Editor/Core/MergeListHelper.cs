using System;
using System.Collections.Generic;

namespace net.bekobeko.utilitytools.core
{
    public enum MergeStatus
    {
        Changed,
        Unchanged,
        Cancelled
    }

    public sealed class MergeResult<T>
    {
        public MergeStatus Status { get; }
        public List<T> Items { get; }

        internal MergeResult(MergeStatus status, List<T> items)
        {
            Status = status;
            Items = items;
        }
    }

    public static class MergeListHelper
    {
        public static MergeResult<T> MergeList<T>(
            IReadOnlyList<T> existing,
            IReadOnlyList<T> generated,
            Func<T, T, bool> keyEquals,
            Func<T, T, bool> valueEquals,
            Func<T, T, ConflictInfo> createConflict,
            ConflictResolutionSession session,
            Func<T, bool> isDeletion = null)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (keyEquals == null) throw new ArgumentNullException(nameof(keyEquals));
            if (valueEquals == null) throw new ArgumentNullException(nameof(valueEquals));
            if (createConflict == null) throw new ArgumentNullException(nameof(createConflict));

            var merged = existing != null ? new List<T>(existing) : new List<T>();
            var changed = false;
            foreach (var generatedItem in generated)
            {
                var deleteItem = isDeletion?.Invoke(generatedItem) == true;
                var existingIndex = FindByKey(merged, generatedItem, keyEquals);
                if (existingIndex < 0)
                {
                    if (!deleteItem)
                    {
                        merged.Add(generatedItem);
                        changed = true;
                    }
                    continue;
                }

                var existingItem = merged[existingIndex];
                if (!deleteItem && valueEquals(existingItem, generatedItem)) continue;

                var action = session?.Resolve(createConflict(existingItem, generatedItem))
                             ?? ConflictResolutionAction.Skip;
                if (action == ConflictResolutionAction.Cancel)
                    return new MergeResult<T>(MergeStatus.Cancelled, merged);
                if (action != ConflictResolutionAction.Overwrite) continue;

                if (deleteItem) merged.RemoveAt(existingIndex);
                else merged[existingIndex] = generatedItem;
                changed = true;
            }

            return new MergeResult<T>(
                changed ? MergeStatus.Changed : MergeStatus.Unchanged,
                merged);
        }

        private static int FindByKey<T>(
            IReadOnlyList<T> items,
            T target,
            Func<T, T, bool> keyEquals)
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (keyEquals(items[index], target)) return index;
            }
            return -1;
        }
    }
}
