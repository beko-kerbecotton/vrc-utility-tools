using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public sealed class MaterialSlotMapping
    {
        public Material Original { get; }
        public int MaterialIndex { get; }
        public Material Replacement { get; set; }
        public bool Enabled { get; set; } = true;

        public MaterialSlotMapping(Material original, int materialIndex)
        {
            Original = original;
            MaterialIndex = materialIndex;
        }
    }

    public sealed class MaterialRendererEntry
    {
        public Renderer Renderer { get; }
        public bool Included { get; set; } = true;
        public IReadOnlyList<MaterialSlotMapping> Slots { get; }

        public MaterialRendererEntry(Renderer renderer, IEnumerable<MaterialSlotMapping> slots)
        {
            Renderer = renderer;
            Slots = slots.ToList();
        }
    }

    public sealed class BulkMaterialState
    {
        public Material Original { get; }
        public int SlotCount { get; }
        public bool Enabled { get; }
        public bool EnabledIsMixed { get; }
        public Material Replacement { get; }
        public bool ReplacementIsMixed { get; }

        public BulkMaterialState(Material original, int slotCount, bool enabled, bool enabledIsMixed,
            Material replacement, bool replacementIsMixed)
        {
            Original = original;
            SlotCount = slotCount;
            Enabled = enabled;
            EnabledIsMixed = enabledIsMixed;
            Replacement = replacement;
            ReplacementIsMixed = replacementIsMixed;
        }
    }

    public sealed class MaterialMappingModel
    {
        private readonly List<MaterialRendererEntry> _renderers;
        private readonly List<Material> _originalMaterials;

        public IReadOnlyList<MaterialRendererEntry> Renderers => _renderers;
        public IReadOnlyList<Material> OriginalMaterials => _originalMaterials;

        public MaterialMappingModel(IEnumerable<MaterialRendererEntry> renderers)
        {
            _renderers = renderers.ToList();
            _originalMaterials = _renderers.SelectMany(entry => entry.Slots)
                .Select(slot => slot.Original).Where(material => material != null).Distinct().ToList();
        }

        public BulkMaterialState GetBulkState(Material original)
        {
            var slots = GetIncludedSlots(original).ToList();
            if (slots.Count == 0) return new BulkMaterialState(original, 0, false, false, null, false);
            var first = slots[0];
            return new BulkMaterialState(original, slots.Count, first.Enabled,
                slots.Any(slot => slot.Enabled != first.Enabled), first.Replacement,
                slots.Any(slot => slot.Replacement != first.Replacement));
        }

        public void SetBulkEnabled(Material original, bool enabled)
        {
            foreach (var slot in GetIncludedSlots(original)) slot.Enabled = enabled;
        }

        public void SetBulkReplacement(Material original, Material replacement)
        {
            foreach (var slot in GetIncludedSlots(original)) slot.Replacement = replacement;
        }

        private IEnumerable<MaterialSlotMapping> GetIncludedSlots(Material original)
        {
            foreach (var renderer in _renderers)
                if (renderer.Included)
                    foreach (var slot in renderer.Slots)
                        if (slot.Original == original) yield return slot;
        }
    }
}
