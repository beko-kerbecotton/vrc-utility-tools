using net.bekobeko.utilitytools.core.scalebake;
using nadena.dev.ndmf;
using UnityEngine;

namespace net.bekobeko.utilitytools.plugin
{
    internal sealed class ScaleBakePass : Pass<ScaleBakePass>
    {
        protected override void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootTransform;
            var result = ScaleBakeSolver.Solve(avatarRoot);
            TransformBaker.Bake(avatarRoot, result);
            RemoveComponents(avatarRoot);
        }

        private static void RemoveComponents(Transform avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<NonUniformScaleBake>(true);
            for (var index = 0; index < components.Length; index++)
            {
                // INDMFEditorOnlyはVRC SDK側でしか除去されないため、Manual Bake向けにプラグイン自身で除去する。
                Object.DestroyImmediate(components[index]);
            }
        }
    }
}

