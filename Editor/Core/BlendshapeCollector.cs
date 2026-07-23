using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public static class BlendshapeCollector
    {
        public static BlendshapeMappingModel CollectModel(
            SkinnedMeshRenderer referenceRenderer,
            GameObject targetRoot)
        {
            var referenceNames = GetBlendshapeNames(referenceRenderer);
            var referenceNameSet = new HashSet<string>(referenceNames, StringComparer.Ordinal);
            var renderers = new List<BlendshapeRendererEntry>();

            if (targetRoot != null)
            {
                foreach (var renderer in targetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == referenceRenderer || renderer.sharedMesh == null) continue;

                    var mappings = new List<RendererBlendshapeMapping>();
                    foreach (var localName in GetBlendshapeNames(renderer).Distinct(StringComparer.Ordinal))
                    {
                        var hasSameName = referenceNameSet.Contains(localName);
                        mappings.Add(new RendererBlendshapeMapping(
                            localName,
                            hasSameName ? localName : null,
                            hasSameName));
                    }

                    if (mappings.Count == 0) continue;
                    renderers.Add(new BlendshapeRendererEntry(renderer, mappings));
                }
            }

            return new BlendshapeMappingModel(referenceRenderer, renderers, referenceNames);
        }

        public static IReadOnlyList<string> GetBlendshapeNames(SkinnedMeshRenderer renderer)
        {
            var names = new List<string>();
            if (renderer == null || renderer.sharedMesh == null) return names;

            for (var index = 0; index < renderer.sharedMesh.blendShapeCount; index++)
            {
                names.Add(renderer.sharedMesh.GetBlendShapeName(index));
            }

            return names;
        }
    }
}
