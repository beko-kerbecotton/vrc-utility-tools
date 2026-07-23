using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public sealed class MaterialSwapEntry
    {
        public Material Original { get; }
        public Material Replacement { get; set; }
        public bool Enabled { get; set; } = true;

        public MaterialSwapEntry(Material original)
        {
            Original = original;
        }
    }

    public static class MaterialSwapCollector
    {
        public static List<MaterialSwapEntry> Collect(GameObject targetRoot)
        {
            var result = new List<MaterialSwapEntry>();
            if (targetRoot == null) return result;

            var collected = new HashSet<Material>();
            foreach (var renderer in targetRoot.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && collected.Add(material))
                        result.Add(new MaterialSwapEntry(material));
                }
            }

            return result;
        }
    }
}
