using System.Collections.Generic;
using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    internal sealed class BakedMeshCache
    {
        private readonly Dictionary<Mesh, List<VertexEntry>> _vertexEntries =
            new Dictionary<Mesh, List<VertexEntry>>();
        private readonly Dictionary<Mesh, List<BindposeEntry>> _bindposeEntries =
            new Dictionary<Mesh, List<BindposeEntry>>();

        internal bool TryGetVertexBaked(Mesh source, Matrix4x4 transformation, out Mesh baked)
        {
            if (_vertexEntries.TryGetValue(source, out var entries))
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    if (MatrixEquals(entries[index].Transformation, transformation))
                    {
                        baked = entries[index].Baked;
                        return true;
                    }
                }
            }
            baked = null;
            return false;
        }

        internal void AddVertexBaked(Mesh source, Matrix4x4 transformation, Mesh baked)
        {
            if (!_vertexEntries.TryGetValue(source, out var entries))
            {
                entries = new List<VertexEntry>();
                _vertexEntries.Add(source, entries);
            }
            entries.Add(new VertexEntry(transformation, baked));
        }

        internal bool TryGetBindposeBaked(Mesh source, Matrix4x4[] bindposes, out Mesh baked)
        {
            if (_bindposeEntries.TryGetValue(source, out var entries))
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    if (MatricesEqual(entries[index].Bindposes, bindposes))
                    {
                        baked = entries[index].Baked;
                        return true;
                    }
                }
            }
            baked = null;
            return false;
        }

        internal void AddBindposeBaked(Mesh source, Matrix4x4[] bindposes, Mesh baked)
        {
            if (!_bindposeEntries.TryGetValue(source, out var entries))
            {
                entries = new List<BindposeEntry>();
                _bindposeEntries.Add(source, entries);
            }
            entries.Add(new BindposeEntry((Matrix4x4[])bindposes.Clone(), baked));
        }

        private static bool MatricesEqual(Matrix4x4[] left, Matrix4x4[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }
            for (var index = 0; index < left.Length; index++)
            {
                if (!MatrixEquals(left[index], right[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool MatrixEquals(Matrix4x4 left, Matrix4x4 right)
        {
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    if (left[row, column] != right[row, column])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private sealed class VertexEntry
        {
            internal readonly Matrix4x4 Transformation;
            internal readonly Mesh Baked;
            internal VertexEntry(Matrix4x4 transformation, Mesh baked)
            {
                Transformation = transformation;
                Baked = baked;
            }
        }

        private sealed class BindposeEntry
        {
            internal readonly Matrix4x4[] Bindposes;
            internal readonly Mesh Baked;
            internal BindposeEntry(Matrix4x4[] bindposes, Mesh baked)
            {
                Bindposes = bindposes;
                Baked = baked;
            }
        }
    }
}
