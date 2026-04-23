using System.Collections.Generic;
using PixelRendering.PixelRenderingFeature;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PixelRendering.RenderPass
{
    public class NormalsPass : ScriptableRenderPass
    {
        private PixelSettings _settings;
        private RenderTextureDescriptor _descriptor;
        
        private int _normalsColorBuffer = Shader.PropertyToID("Normal Color RT");
        private int _normalsDepthBuffer = Shader.PropertyToID("Normal Depth RT");
        private int _intermediateColorBuffer = Shader.PropertyToID("Intermediate Color RT");

        private RTHandle _cameraColorBuffer;
        
        private List<ShaderTagId> _shaderTagIdList;
        private FilteringSettings _filterSettings;
        private ProfilingSampler _profilingSampler;
        
        public NormalsPass(PixelSettings settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            _settings = settings;
            
            _shaderTagIdList = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            
            _filterSettings = new FilteringSettings(RenderQueueRange.opaque);
            _profilingSampler = new ProfilingSampler("Pixel Rendering Normals Pass");
        }

        public void Setup(RTHandle color, RTHandle depth)
        {
            _cameraColorBuffer = color;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Only run for the pixel camera that renders to our specific RT.
            // This prevents this pass from executing for reflection cameras,
            // the MainCamera, or any other camera that goes through this renderer.
            if (renderingData.cameraData.camera.targetTexture != _settings.rt)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            int width = _settings.width;
            int height = _settings.height;
            
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.GetTemporaryRT(_normalsColorBuffer, width , height, 0, FilterMode.Point, GraphicsFormat.R8G8B8A8_UNorm);
                cmd.GetTemporaryRT(_normalsDepthBuffer, width, height , 32, FilterMode.Point, RenderTextureFormat.Depth);
                
                cmd.SetRenderTarget(
                    _normalsColorBuffer, 
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    _normalsDepthBuffer,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                
                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
                DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIdList, ref renderingData, sortingCriteria);
                drawingSettings.overrideMaterial = _settings.NormalPassMat;
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filterSettings);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                // Expose depth and opaque color for shaders like Stylized Water
                cmd.SetGlobalTexture("_CameraDepthTexture", _normalsDepthBuffer);
                cmd.SetGlobalTexture("_CameraOpaqueTexture", _cameraColorBuffer);
                cmd.SetGlobalTexture("_CameraNormalsTexture", _normalsColorBuffer);
                
                cmd.SetGlobalTexture("_NormalsPassTexture", _normalsColorBuffer);
                cmd.SetGlobalTexture("_DepthTexture", _normalsDepthBuffer);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                cmd.GetTemporaryRT(_intermediateColorBuffer, width , height, 0, FilterMode.Point, UnityEngine.RenderTextureFormat.DefaultHDR);
                cmd.SetRenderTarget(_intermediateColorBuffer, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                cmd.ClearRenderTarget(false, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                _settings.OutlineBlitMaterial.SetFloat("_depthEdgeStrength", _settings.depthEdgeStrength);
                _settings.OutlineBlitMaterial.SetFloat("_normalEdgeStrength", _settings.normalEdgeStrength);
                cmd.Blit(_cameraColorBuffer, _intermediateColorBuffer, _settings.OutlineBlitMaterial);
                cmd.Blit(_intermediateColorBuffer, _cameraColorBuffer);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                // We keep _normalsDepthBuffer and _normalsColorBuffer until the end of the camera rendering 
                // so subsequent transparent passes (like Water) can use them.
                // URP will automatically clean up these TemporaryRTs at the end of the camera loop.
                cmd.ReleaseTemporaryRT(_intermediateColorBuffer);
            }
            
            context.ExecuteCommandBuffer(cmd); 
            CommandBufferPool.Release(cmd);
        }
    }
}