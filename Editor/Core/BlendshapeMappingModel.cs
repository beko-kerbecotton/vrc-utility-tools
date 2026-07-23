using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public sealed class RendererBlendshapeMapping
    {
        public string LocalBlendshape { get; }
        public string ReferenceBlendshape { get; set; }
        public bool Enabled { get; set; }

        public RendererBlendshapeMapping(string localBlendshape, string referenceBlendshape, bool enabled)
        {
            LocalBlendshape = localBlendshape;
            ReferenceBlendshape = referenceBlendshape;
            Enabled = enabled;
        }
    }

    public sealed class BlendshapeRendererEntry
    {
        private readonly Dictionary<string, RendererBlendshapeMapping> _mappings;

        public SkinnedMeshRenderer Renderer { get; }
        public bool Included { get; set; } = true;
        public IReadOnlyDictionary<string, RendererBlendshapeMapping> Mappings => _mappings;

        public BlendshapeRendererEntry(
            SkinnedMeshRenderer renderer,
            IEnumerable<RendererBlendshapeMapping> mappings)
        {
            Renderer = renderer;
            _mappings = mappings.ToDictionary(mapping => mapping.LocalBlendshape, StringComparer.Ordinal);
        }

        public bool TryGetMapping(string localBlendshape, out RendererBlendshapeMapping mapping)
        {
            return _mappings.TryGetValue(localBlendshape, out mapping);
        }
    }

    public sealed class BulkBlendshapeState
    {
        public string LocalBlendshape { get; }
        public int RendererCount { get; }
        public bool Enabled { get; }
        public bool EnabledIsMixed { get; }
        public string ReferenceBlendshape { get; }
        public bool ReferenceIsMixed { get; }

        public BulkBlendshapeState(
            string localBlendshape,
            int rendererCount,
            bool enabled,
            bool enabledIsMixed,
            string referenceBlendshape,
            bool referenceIsMixed)
        {
            LocalBlendshape = localBlendshape;
            RendererCount = rendererCount;
            Enabled = enabled;
            EnabledIsMixed = enabledIsMixed;
            ReferenceBlendshape = referenceBlendshape;
            ReferenceIsMixed = referenceIsMixed;
        }
    }

    public sealed class BlendshapeMappingModel
    {
        private readonly List<BlendshapeRendererEntry> _renderers;
        private readonly List<string> _localBlendshapes;

        public SkinnedMeshRenderer ReferenceRenderer { get; }
        public IReadOnlyList<string> ReferenceBlendshapes { get; }
        public IReadOnlyList<BlendshapeRendererEntry> Renderers => _renderers;
        public IReadOnlyList<string> LocalBlendshapes => _localBlendshapes;

        public BlendshapeMappingModel(
            SkinnedMeshRenderer referenceRenderer,
            IEnumerable<BlendshapeRendererEntry> renderers,
            IEnumerable<string> referenceBlendshapes)
        {
            ReferenceRenderer = referenceRenderer;
            _renderers = renderers.ToList();
            ReferenceBlendshapes = referenceBlendshapes.ToList();
            _localBlendshapes = _renderers
                .SelectMany(renderer => renderer.Mappings.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        public BulkBlendshapeState GetBulkState(string localBlendshape)
        {
            var mappings = GetIncludedMappings(localBlendshape).ToList();
            if (mappings.Count == 0)
                return new BulkBlendshapeState(localBlendshape, 0, false, false, null, false);

            var first = mappings[0];
            return new BulkBlendshapeState(
                localBlendshape,
                mappings.Count,
                first.Enabled,
                mappings.Any(mapping => mapping.Enabled != first.Enabled),
                first.ReferenceBlendshape,
                mappings.Any(mapping => !string.Equals(
                    mapping.ReferenceBlendshape,
                    first.ReferenceBlendshape,
                    StringComparison.Ordinal)));
        }

        public void SetBulkEnabled(string localBlendshape, bool enabled)
        {
            foreach (var mapping in GetIncludedMappings(localBlendshape)) mapping.Enabled = enabled;
        }

        public void SetBulkReference(string localBlendshape, string referenceBlendshape)
        {
            foreach (var mapping in GetIncludedMappings(localBlendshape))
                mapping.ReferenceBlendshape = referenceBlendshape;
        }

        private IEnumerable<RendererBlendshapeMapping> GetIncludedMappings(string localBlendshape)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer.Included && renderer.TryGetMapping(localBlendshape, out var mapping))
                    yield return mapping;
            }
        }
    }
}
