using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SandSystem
{
    /// <summary>
    /// Fullscreen heat-haze distortion for URP (Unity 6 Render Graph only).
    /// Add to Universal Renderer Data (PC_Renderer / Mobile_Renderer).
    /// </summary>
    public class HeatDistortionRendererFeature : ScriptableRendererFeature
    {
        public enum InjectionPoint
        {
            BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,
            BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
            AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing
        }

        public enum SourceMode
        {
            /// <summary>Copy active color and distort (includes airborne sand). Recommended.</summary>
            BlitColor = 0,
            /// <summary>Sample _CameraOpaqueTexture. Requires Opaque Texture On.</summary>
            OpaqueTexture = 1
        }

        [System.Serializable]
        public class Settings
        {
            public bool isEnabled = true;
            public InjectionPoint injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;
            public SourceMode sourceMode = SourceMode.BlitColor;
            public Material material;

            [Header("Distortion")]
            [Range(0f, 0.1f)] public float distortionStrength = 0.012f;
            public float noiseScale = 8f;
            public float scrollSpeed = 0.15f;
            public Vector2 windDirection = new Vector2(1f, 0.2f);
            [Range(0f, 1f)] public float verticalBias = 0.65f;
            public Texture2D noiseTexture;

            [Header("Mask")]
            public bool useDepthMask = true;
            public float depthMaskStart = 5f;
            public float depthMaskEnd = 80f;
            public float heatHeightStart = 0.5f;
            public float heatHeightEnd = 12f;
            [Range(0.2f, 4f)] public float heatMaskPower = 1.2f;
        }

        public Settings settings = new Settings();

        public static HeatDistortionRendererFeature Instance { get; private set; }

        HeatDistortionPass _pass;

        static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
        static readonly int VerticalBiasId = Shader.PropertyToID("_VerticalBias");
        static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        static readonly int UseDepthMaskId = Shader.PropertyToID("_UseDepthMask");
        static readonly int DepthMaskStartId = Shader.PropertyToID("_DepthMaskStart");
        static readonly int DepthMaskEndId = Shader.PropertyToID("_DepthMaskEnd");
        static readonly int HeatHeightStartId = Shader.PropertyToID("_HeatHeightStart");
        static readonly int HeatHeightEndId = Shader.PropertyToID("_HeatHeightEnd");
        static readonly int HeatMaskPowerId = Shader.PropertyToID("_HeatMaskPower");

        public override void Create()
        {
            Instance = this;
            _pass = new HeatDistortionPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.isEnabled || settings.material == null)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview
                || renderingData.cameraData.cameraType == CameraType.Reflection
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            PushMaterialParams();

            bool useOpaque = settings.sourceMode == SourceMode.OpaqueTexture;
            _pass.renderPassEvent = (RenderPassEvent)settings.injectionPoint;
            _pass.Setup(settings.material, useOpaque);

            if (useOpaque)
                _pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            else
                _pass.ConfigureInput(ScriptableRenderPassInput.Depth);

            _pass.requiresIntermediateTexture = !useOpaque;
            renderer.EnqueuePass(_pass);
        }

        void PushMaterialParams()
        {
            var mat = settings.material;
            mat.SetFloat(DistortionStrengthId, settings.distortionStrength);
            mat.SetFloat(NoiseScaleId, settings.noiseScale);
            mat.SetFloat(ScrollSpeedId, settings.scrollSpeed);
            mat.SetVector(WindDirectionId, new Vector4(settings.windDirection.x, settings.windDirection.y, 0f, 0f));
            mat.SetFloat(VerticalBiasId, settings.verticalBias);
            mat.SetFloat(UseDepthMaskId, settings.useDepthMask ? 1f : 0f);
            mat.SetFloat(DepthMaskStartId, settings.depthMaskStart);
            mat.SetFloat(DepthMaskEndId, settings.depthMaskEnd);
            mat.SetFloat(HeatHeightStartId, settings.heatHeightStart);
            mat.SetFloat(HeatHeightEndId, settings.heatHeightEnd);
            mat.SetFloat(HeatMaskPowerId, settings.heatMaskPower);

            if (settings.noiseTexture != null)
                mat.SetTexture(NoiseTexId, settings.noiseTexture);

            if (settings.sourceMode == SourceMode.OpaqueTexture)
            {
                mat.EnableKeyword("_SOURCEMODE_OPAQUETEXTURE");
                mat.DisableKeyword("_SOURCEMODE_BLITCOLOR");
            }
            else
            {
                mat.EnableKeyword("_SOURCEMODE_BLITCOLOR");
                mat.DisableKeyword("_SOURCEMODE_OPAQUETEXTURE");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Instance == this)
                Instance = null;
            _pass?.Dispose();
        }

        sealed class HeatDistortionPass : ScriptableRenderPass
        {
            static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
            static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

            Material _material;
            bool _useOpaqueTexture;

            public HeatDistortionPass()
            {
                profilingSampler = new ProfilingSampler("Heat Distortion");
            }

            public void Setup(Material material, bool useOpaqueTexture)
            {
                _material = material;
                _useOpaqueTexture = useOpaqueTexture;
            }

            public void Dispose()
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraColor.IsValid())
                    return;

                TextureHandle copied = TextureHandle.nullHandle;

                if (!_useOpaqueTexture)
                {
                    var desc = renderGraph.GetTextureDesc(resources.cameraColor);
                    desc.name = "_HeatDistortionColorCopy";
                    desc.clearBuffer = false;
                    copied = renderGraph.CreateTexture(desc);

                    using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Heat Distortion Copy", out var data, profilingSampler))
                    {
                        data.source = resources.activeColorTexture;
                        builder.UseTexture(data.source, AccessFlags.Read);
                        builder.SetRenderAttachment(copied, 0, AccessFlags.Write);
                        builder.SetRenderFunc(static (CopyPassData d, RasterGraphContext ctx) =>
                        {
                            Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                        });
                    }
                }

                using (var builder = renderGraph.AddRasterRenderPass<MainPassData>("Heat Distortion", out var data, profilingSampler))
                {
                    data.material = _material;
                    data.source = copied;

                    if (copied.IsValid())
                        builder.UseTexture(copied, AccessFlags.Read);

                    if (_useOpaqueTexture && resources.cameraOpaqueTexture.IsValid())
                        builder.UseTexture(resources.cameraOpaqueTexture, AccessFlags.Read);

                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (MainPassData d, RasterGraphContext ctx) =>
                    {
                        RTHandle src = d.source.IsValid() ? d.source : null;
                        DrawFullscreen(ctx.cmd, d.material, src);
                    });
                }
            }

            static void DrawFullscreen(RasterCommandBuffer cmd, Material material, RTHandle source)
            {
                Block.Clear();
                if (source != null)
                    Block.SetTexture(BlitTextureId, source);
                Block.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, Block);
            }

            class CopyPassData
            {
                public TextureHandle source;
            }

            class MainPassData
            {
                public Material material;
                public TextureHandle source;
            }
        }
    }
}
