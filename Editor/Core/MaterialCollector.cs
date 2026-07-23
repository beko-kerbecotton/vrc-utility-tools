using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core
{
    public static class MaterialCollector
    {
        public static MaterialMappingModel CollectModel(GameObject targetRoot)
        {
            var entries = new List<MaterialRendererEntry>();
            if (targetRoot == null) return new MaterialMappingModel(entries);

            foreach (var renderer in targetRoot.GetComponentsInChildren<Renderer>(true))
            {
                var slots = new List<MaterialSlotMapping>();
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    if (materials[index] != null) slots.Add(new MaterialSlotMapping(materials[index], index));
                }
                if (slots.Count > 0) entries.Add(new MaterialRendererEntry(renderer, slots));
            }

            return new MaterialMappingModel(entries);
        }
    }
}
