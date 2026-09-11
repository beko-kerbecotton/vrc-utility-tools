using UnityEngine;

namespace net.bekobeko.utilitytools.core.scalebake
{
    internal static class ScaleBakeMatrixUtility
    {
        internal static bool IsIdentity(Matrix4x4 matrix)
        {
            var identity = Matrix4x4.identity;
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    if (matrix[row, column] != identity[row, column]) return false;
                }
            }

            return true;
        }

        internal static Matrix4x4 InverseTranspose(Matrix4x4 matrix)
        {
            // アフィン行列全体の逆転置の左上3x3は、線形部の逆転置と一致する。
            return matrix.inverse.transpose;
        }
    }
}
