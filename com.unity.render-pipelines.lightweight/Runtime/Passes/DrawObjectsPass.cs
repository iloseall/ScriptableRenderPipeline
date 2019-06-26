using System.Collections.Generic;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.LWRP
{
    /// <summary>
    /// Draw  objects into the given color and depth target
    ///
    /// You can use this pass to render objects that have a material and/or shader
    /// with the pass names LightweightForward or SRPDefaultUnlit.
    /// </summary>
    internal class DrawObjectsPass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        string m_ProfilerTag;
        bool m_IsOpaque;

        public DrawObjectsPass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            m_ProfilerTag = profilerTag;
            m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_IsOpaque = opaque;

            if (stencilState.enabled)
            {
                m_RenderStateBlock.stencilReference = stencilReference;
                m_RenderStateBlock.mask = RenderStateMask.Stencil;
                m_RenderStateBlock.stencilState = stencilState;
            }
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                var sortFlags = (m_IsOpaque)
                    ? renderingData.cameraData.defaultOpaqueSortFlags
                    : SortingCriteria.CommonTransparent;
                
                bool isMaterialDebugActive = lightingDebugMode != LightingDebugMode.None ||
                                             debugMaterialIndex != DebugMaterialIndex.None;

                var fullScreenDebugMode = DebugDisplaySettings.Instance.buffer.FullScreenDebugMode;
                bool isReplacementDebugActive = fullScreenDebugMode == FullScreenDebugMode.Overdraw || fullScreenDebugMode == FullScreenDebugMode.Wireframe || fullScreenDebugMode == FullScreenDebugMode.SolidWireframe;
                if (isMaterialDebugActive || isReplacementDebugActive)
                {
                    if(lightingDebugMode == LightingDebugMode.ShadowCascades)
                        // we disable cubemap reflections, too distracting (in TemplateLWRP for ex.)
                        cmd.EnableShaderKeyword("_DEBUG_ENVIRONMENTREFLECTIONS_OFF");
                    else
                        cmd.DisableShaderKeyword("_DEBUG_ENVIRONMENTREFLECTIONS_OFF");

                    DebugReplacementPassType debugPassType;
                    
                    if (isMaterialDebugActive)
                        debugPassType = DebugReplacementPassType.None;
                    else
                    {
                        switch(fullScreenDebugMode)
                        {
                            case FullScreenDebugMode.Overdraw:
                                debugPassType = DebugReplacementPassType.Overdraw;
                                break;
                            case FullScreenDebugMode.Wireframe:
                                debugPassType = DebugReplacementPassType.Wireframe;
                                break;
                            case FullScreenDebugMode.SolidWireframe:
                                debugPassType = DebugReplacementPassType.SolidWireframe;
                                break;
                            default:
                                debugPassType = DebugReplacementPassType.None;
                                break;
                        }
                    }
                    
                    RenderingUtils.RenderObjectWithDebug(context, ref renderingData, camera,
                        debugPassType, m_FilteringSettings, sortFlags);
                }
                else
                {
                    var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings,
                        ref m_RenderStateBlock);

                    // Render objects that did not match any shader pass with error shader
                    RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera,
                        m_FilteringSettings, SortingCriteria.None);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
