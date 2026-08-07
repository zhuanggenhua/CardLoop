using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CryingSnow.StackCraft
{
    public class CustomPostProcessFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material effectMaterial;
            public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public Settings settings = new Settings();
        CustomPass customPass;

        public override void Create()
        {
            customPass = new CustomPass(settings.effectMaterial);
            customPass.renderPassEvent = settings.renderEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.effectMaterial == null) return;
            renderer.EnqueuePass(customPass);
        }

        class CustomPass : ScriptableRenderPass
        {
            Material material;

            public CustomPass(Material mat)
            {
                this.material = mat;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle source = resourceData.cameraColor;
                if (!source.IsValid()) return;

                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_TempCustomPostProcess";
                desc.clearBuffer = false;

                TextureHandle destination = renderGraph.CreateTexture(desc);
                RenderGraphUtils.BlitMaterialParameters blitParameters = new(source, destination, material, 0);

                renderGraph.AddBlitPass(blitParameters, "Custom Post Process");
                resourceData.cameraColor = destination;
            }
        }
    }
}
