using nadena.dev.ndmf;
using UnityEngine;

namespace net.bekobeko.utilitytools
{
    /// <summary>
    /// 非一様スケールをTransform階層に残さず、NDMFビルド時にボーンのレスト位置とMesh形状へ
    /// Bakeするためのマーカーコンポーネントです。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("KerBekoShop/UtilityTools/Non-Uniform Scale Bake")]
    public sealed class NonUniformScaleBake : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField] private Vector3 _scale = Vector3.one;

        public Vector3 Scale
        {
            get => _scale;
            set => _scale = value;
        }
    }
}
