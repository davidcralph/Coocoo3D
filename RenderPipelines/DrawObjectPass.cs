﻿using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class DrawObjectPass : Pass
    {
        public string shader;

        public List<(string, string)> keywords = new();
        List<(string, string)> keywords2 = new();

        public List<(string, string)> AutoKeyMap = new();

        public PSODesc psoDesc;

        public bool enableVS = true;
        public bool enablePS = true;
        public bool enableGS = false;

        public string rs;

        public bool clearRenderTarget = false;
        public bool clearDepth = false;

        public Rectangle? scissorViewport;

        public object[] CBVPerObject;

        public object[] CBVPerPass;

        public Func<RenderWrap, MeshRenderable, List<(string, string)>, bool> filter;

        public override void Execute(RenderWrap renderWrap)
        {
            renderWrap.SetRootSignature(rs);
            renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
            if (scissorViewport != null)
            {
                var rect = scissorViewport.Value;
                renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
            var desc = GetPSODesc(renderWrap, psoDesc);

            var writer = renderWrap.Writer;
            writer.Clear();
            if (CBVPerPass != null)
            {
                renderWrap.Write(CBVPerPass, writer);
                writer.SetBufferImmediately(2);
            }

            keywords2.Clear();
            foreach (var renderable in renderWrap.MeshRenderables())
            {
                keywords2.AddRange(this.keywords);
                if (filter != null && !filter.Invoke(renderWrap, renderable, keywords2)) continue;
                foreach (var keyMap in AutoKeyMap)
                {
                    if (true.Equals(renderWrap.GetIndexableValue(keyMap.Item1, renderable.material)))
                        keywords2.Add((keyMap.Item2, "1"));
                }
                if (renderable.gpuSkinning)
                {
                    keywords2.Add(new("SKINNING", "1"));
                }
                if (renderable.material.DrawDoubleFace)
                    desc.cullMode = CullMode.None;
                else
                    desc.cullMode = CullMode.Back;
                renderWrap.SetShader(shader, desc, keywords2, enableVS, enablePS, enableGS);

                if (renderable.gpuSkinning)
                    renderWrap.SetCBV(renderWrap.GetBoneBuffer(), 0);

                CBVPerObject[0] = renderable.transform;

                renderWrap.Write(CBVPerObject, writer, renderable.material);
                writer.SetBufferImmediately(1);

                renderWrap.SetSRVs(srvs, renderable.material);

                renderWrap.Draw(renderable);
                keywords2.Clear();
            }
        }
    }
}