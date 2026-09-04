using nadena.dev.ndmf;
using net.bekobeko.utilitytools.plugin;

[assembly: ExportsPlugin(typeof(ScaleBakePluginDefinition))]

namespace net.bekobeko.utilitytools.plugin
{
    public class ScaleBakePluginDefinition : Plugin<ScaleBakePluginDefinition>
    {
        public override string QualifiedName => "net.bekobeko.utilitytools";

        public override string DisplayName => "Beko's Utility Tools";

        protected override void Configure()
        {
            // 構造生成・統合の後、最適化より前、かつFloorAdjusterによるアバター移動より前に実行する。
            // late-transform-stagesはMA本体とは別のプラグインであり、FloorAdjusterを担当する。
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .BeforePlugin("nadena.dev.modular-avatar.late-transform-stages")
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run(ScaleBakePass.Instance);
        }
    }
}
