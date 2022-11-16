﻿using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using FireRender.AMD.RenderEngine.Core;
using ProRendererWrap;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines;

[UIShow(name: "Radeon ProRender")]
public class RadeonProRender : RenderPipeline, IDisposable
{
    [AOV(AOVType.Color)]
    [Size("Output")]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    public Texture2D output;

    [Size("Output")]
    [Format(ResourceFormat.R32G32B32A32_Float)]
    public Texture2D noPostProcess;

    [UIShow(name: "Skybox Texture")]
    [Srgb]
    [Resource("adams_place_bridge_2k.jpg")]
    public Texture2D skyboxTexture;


    [Size(32, 32)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D intermedia2;

    [UIDragFloat(0.01f, 0, name: "Sky Light Muliplier")]
    public float SkyLightMultiple = 3;


    [UIDragFloat(0.1f, 0.1f, 60, name: "Directional Light Shadow Softness Angle")]
    public float DirectionalLightShadowSoftnessAngle = 0.5f;

    public int ViewportSampleCount = 2;

    public int RecordSampleCount = 128;

    #region Material
    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "Metallic")]
    public float Metallic;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "Roughness")]
    public float Roughness = 0.8f;

    [Indexable]
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Material, "Emissive")]
    public float Emissive;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "Specular")]
    public float Specular = 0.5f;

    [Indexable]
    [UIShow(UIShowType.Material, "Refraction")]
    public bool Refraction = false;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "IOR")]
    public float IOR = 1.44f;


    [UIShow(UIShowType.Material)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    [Srgb]
    public Texture2D _Albedo;

    [UIShow(UIShowType.Material)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Metallic;

    [UIShow(UIShowType.Material)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Roughness;

    [UIShow(UIShowType.Material)]
    [Srgb]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Emissive;
    #endregion

    #region Light Parameters
    [Indexable]
    [UIColor(UIShowType.Light, "Light Colour")]
    public Vector3 LightColor = new Vector3(3, 3, 3);
    [Indexable]
    [UIDragFloat(0.1f, 0.1f, float.MaxValue, UIShowType.Light, "Light Range")]
    public float LightRange = 50.0f;

    [Indexable]
    [UIShow(UIShowType.Light, "Light Type")]
    public LightType LightType;
    #endregion

    [SceneCapture("Camera")]
    public CameraData Camera;

    [SceneCapture("Recording")]
    public bool Recording;

    [SceneCapture("Visual")]
    public IEnumerable<VisualComponent> Visuals;

    RPRContext context;
    RPRScene scene;
    RPRCamera rprCamera;
    RPRMaterialSystem materialSystem;

    string skyBoxFile;
    RPRImage skyBoxImage;
    RPRFrameBuffer frameBuffer;
    RPRFrameBuffer frameBufferResolved;

    Dictionary<string, RPRImage> images = new Dictionary<string, RPRImage>();

    byte[] frameBuffer1 = new byte[2048 * 2048 * 16];

    RenderHelper renderHelper;

    Vector2 size;
    public DrawQuadPass postProcess = new DrawQuadPass()
    {
        shader = "PostProcessing.hlsl",
        rs = "Css",
        renderTargets = new string[]
        {
            nameof(output)
        },
        //depthStencil = null,
        psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
        },
        srvs = new string[]
        {
            nameof(noPostProcess),
            nameof(intermedia2),
        },
        cbvs = new object[][]
        {
            new object []
            {

            }
        }
    };

    public RadeonProRender()
    {
        context = new RPRContext();
        Console.WriteLine("Radeon Prorender.");
        Console.WriteLine(context.GetInfo(Rpr.ContextInfo.GPU0_NAME));

        rprCamera = new RPRCamera(context);

        materialSystem = new RPRMaterialSystem(context);
    }

    public override void BeforeRender()
    {
        renderHelper ??= new RenderHelper();
        renderHelper.renderWrap = renderWrap;
        renderHelper.CPUSkinning = true;
        renderHelper.CPUOnlySkinning();

        renderWrap.GetOutputSize(out int width, out int height);
        renderWrap.SetSize("Output", width, height);
        renderHelper.PushParameters(this);
    }

    public override void Render()
    {
        string skyBoxFile1 = renderWrap.GetTex2DPath(nameof(skyboxTexture));
        if (skyBoxFile != skyBoxFile1)
        {
            skyBoxFile = skyBoxFile1;
            skyBoxImage?.Dispose();
            skyBoxImage = new RPRImage(context, skyBoxFile);
        }

        if (size != new Vector2(output.width, output.height))
        {
            size = new Vector2(output.width, output.height);
            var desc = new Rpr.FrameBufferDesc()
            {
                FbWidth = (uint)size.X,
                FbHeight = (uint)size.Y
            };
            var format = new Rpr.FramebufferFormat()
            {
                NumComponents = 4,
                Type = (uint)Rpr.ComponentType.FLOAT32
            };
            frameBuffer?.Dispose();
            frameBufferResolved?.Dispose();
            frameBuffer = new RPRFrameBuffer(context, format, desc);
            frameBufferResolved = new RPRFrameBuffer(context, format, desc);
        }

        var camera = this.Camera;
        scene = new RPRScene(context);
        context.SetScene(scene);
        Vector3 angle = camera.Angle;
        rprCamera.SetNearPlane(camera.near);
        rprCamera.SetFarPlane(camera.far);
        rprCamera.LookAt(camera.Position, camera.LookAtPoint, Vector3.Transform(Vector3.UnitY, Matrix4x4.CreateFromYawPitchRoll(-angle.Y, -angle.X, -angle.Z)));
        scene.SetCamera(rprCamera);

        List<RPRShape> shapes = new List<RPRShape>();
        List<RPRLight> lights = new List<RPRLight>();
        List<RPRMaterialNode> materialNodes = new List<RPRMaterialNode>();
        var envLight = RPRLight.EnvLight(context);
        envLight.EnvironmentLightSetImage(skyBoxImage);
        envLight.EnvironmentLightSetIntensityScale(SkyLightMultiple / (float)Math.PI);
        envLight.SetTransform(Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateRotationY((float)Math.PI));
        scene.AttachLight(envLight);
        lights.Add(envLight);

        foreach (var visual in Visuals)
        {
            var material = visual.material;
            if (visual.UIShowType == Caprice.Display.UIShowType.Light)
            {
                var lightType = (LightType)renderHelper.GetIndexableValue("LightType", material);
                if (lightType == LightType.Directional)
                {
                    var Color = (Vector3)renderHelper.GetIndexableValue("LightColor", material);
                    var Direction = Vector3.Transform(-Vector3.UnitZ, visual.transform.rotation);
                    var Rotation = visual.transform.rotation;

                    var light1 = RPRLight.DirectionalLight(context);
                    light1.SetTransform(Matrix4x4.CreateLookAt(Direction, new Vector3(0, 0, 0), new Vector3(0, 1, 0)));
                    light1.DirectionalLightSetRadiantPower3f(Color.X, Color.Y, Color.Z);
                    light1.DirectionalLightSetShadowSoftnessAngle(DirectionalLightShadowSoftnessAngle / 180.0f * (float)Math.PI);
                    scene.AttachLight(light1);
                    lights.Add(light1);
                }
                else if (lightType == LightType.Point)
                {

                    var Color = (Vector3)renderHelper.GetIndexableValue("LightColor", material);
                    var Position = visual.transform.position;
                    var Range = (float)renderHelper.GetIndexableValue("LightRange", material);

                    var light1 = RPRLight.PointLight(context);
                    light1.SetTransform(Matrix4x4.CreateTranslation(Position));
                    light1.PointLightSetRadiantPower3f(Color.X, Color.Y, Color.Z);
                    scene.AttachLight(light1);
                    lights.Add(light1);
                }
            }
        }

        foreach (var renderable in renderHelper.MeshRenderables(false))
        {
            renderable.mesh.TryGetBuffer(0, out byte[] vertexBuf);
            renderable.mesh.TryGetBuffer(1, out byte[] normalBuf);
            renderable.mesh.TryGetBuffer(2, out byte[] uvBuf);
            if (!renderable.gpuSkinning && renderable.meshOverride != null)
            {
                renderable.meshOverride.TryGetBuffer(0, out vertexBuf);
                renderable.meshOverride.TryGetBuffer(1, out normalBuf);
            }
            var indiceBuf = renderable.mesh.m_indexData;
            var vertStart = renderable.vertexStart;
            var vertCount = renderable.vertexCount;
            var indexStart = renderable.indexStart;
            var indexCount = renderable.indexCount;

            RPRShape shape = new RPRShape(context,
                new Span<byte>(vertexBuf, vertStart * 12, vertCount * 12),
                new Span<byte>(normalBuf, vertStart * 12, vertCount * 12),
                new Span<byte>(uvBuf, vertStart * 8, vertCount * 8),
                new Span<byte>(indiceBuf, indexStart * 4, indexCount * 4), indexCount, true);
            shape.SetTransform(renderable.transform);
            scene.AttachShape(shape);
            shapes.Add(shape);
            RPRMaterialNode materialNode = new RPRMaterialNode(materialSystem, Rpr.MaterialNodeType.UBERV2);
            RPRMaterialNode colorTexNode = GetImageNode(renderWrap.GetTex2DPath(nameof(_Albedo), renderable.material));

            RPRMaterialNode roughnessTexNode = GetImageNode(renderWrap.GetTex2DPath(nameof(_Roughness), renderable.material));
            RPRMaterialNode metallicTexNode = GetImageNode(renderWrap.GetTex2DPath(nameof(_Metallic), renderable.material));
            RPRMaterialNode emissiveTexNode = GetImageNode(renderWrap.GetTex2DPath(nameof(_Emissive), renderable.material));

            materialNodes.Add(colorTexNode);
            materialNodes.Add(materialNode);

            float Metallic = (float)renderHelper.GetIndexableValue(nameof(this.Metallic), renderable.material);
            float Roughness = (float)renderHelper.GetIndexableValue(nameof(this.Roughness), renderable.material);
            float Emissive = (float)renderHelper.GetIndexableValue(nameof(this.Emissive), renderable.material);
            bool Refraction = (bool)renderHelper.GetIndexableValue(nameof(this.Refraction), renderable.material);
            float IOR = (float)renderHelper.GetIndexableValue(nameof(this.IOR), renderable.material);

            materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_DIFFUSE_COLOR, colorTexNode);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_DIFFUSE_ROUGHNESS, Roughness, Roughness, Roughness, Roughness);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_DIFFUSE_WEIGHT, 1.0f, 1.0f, 1.0f, 1.0f);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_IOR, IOR, IOR, IOR, IOR);
            materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFLECTION_COLOR, colorTexNode);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_METALNESS, Metallic, Metallic, Metallic, Metallic);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_ROUGHNESS, Roughness, Roughness, Roughness, Roughness);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_WEIGHT, 1.0f, 1.0f, 1.0f, 1.0f);
            if (!Refraction)
                materialNode.SetInputUByKey(Rpr.MaterialInput.UBER_REFLECTION_MODE, (uint)Rpr.UberMaterialMode.METALNESS);
            if (roughnessTexNode != null)
            {
                materialNodes.Add(roughnessTexNode);
                RPRMaterialNode selectY = new RPRMaterialNode(materialSystem, Rpr.MaterialNodeType.ARITHMETIC);
                selectY.SetInputUByKey(Rpr.MaterialInput.OP, (uint)Rpr.MaterialNodeOp.SELECT_Y);
                selectY.SetInputNByKey(Rpr.MaterialInput.COLOR0, roughnessTexNode);
                materialNodes.Add(selectY);

                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_DIFFUSE_ROUGHNESS, selectY);
                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFLECTION_ROUGHNESS, selectY);
            }
            if (metallicTexNode != null)
            {
                materialNodes.Add(metallicTexNode);
                RPRMaterialNode selectZ = new RPRMaterialNode(materialSystem, Rpr.MaterialNodeType.ARITHMETIC);
                selectZ.SetInputUByKey(Rpr.MaterialInput.OP, (uint)Rpr.MaterialNodeOp.SELECT_Z);
                selectZ.SetInputNByKey(Rpr.MaterialInput.COLOR0, metallicTexNode);
                materialNodes.Add(selectZ);
                if (!Refraction)
                    materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFLECTION_METALNESS, selectZ);
            }
            if (emissiveTexNode != null)
            {
                materialNodes.Add(emissiveTexNode);
                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_EMISSION_COLOR, emissiveTexNode);
                //materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_EMISSION_WEIGHT, Emissive, Emissive, Emissive, Emissive);
                //materialNode.SetInputUByKey(Rpr.MaterialInput.UBER_EMISSION_MODE,Rpr.UberMaterialEmissionMode.);
            }
            if (Refraction)
            {
                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFRACTION_COLOR, colorTexNode);
                //materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFRACTION_COLOR, refractionColor.X, refractionColor.Y, refractionColor.Z, 1);
                materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFRACTION_IOR, IOR, IOR, IOR, IOR);
                materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFRACTION_WEIGHT, 1, 1, 1, 1);
            }
            shape.SetMaterial(materialNode);
        }


        context.SetParameterByKey1u(Rpr.ContextInfo.TONE_MAPPING_TYPE, (uint)Rpr.ToneMappingOperator.NONE);
        context.SetParameterByKey1u(Rpr.ContextInfo.ITERATIONS, Recording ? (uint)RecordSampleCount : (uint)ViewportSampleCount);
        frameBuffer.Clear();
        frameBufferResolved.Clear();
        context.SetAOV(Rpr.Aov.COLOR, frameBuffer);
        context.Render();
        context.ResolveFrameBuffer(frameBuffer, frameBufferResolved, false);
        frameBufferResolved.GetInfo(Rpr.FrameBuffer.DATA, frameBuffer1, out int dataSize);

        scene.Clear();
        foreach (var shape in shapes)
        {
            scene.DetachShape(shape);
            shape.Dispose();
        }
        foreach (var light in lights)
        {
            scene.DetachLight(light);
            light.Dispose();
        }
        foreach (var materialNode in materialNodes)
        {
            materialNode.Dispose();
        }
        scene.Dispose();
        renderWrap.graphicsContext.UploadTexture(noPostProcess, frameBuffer1);

        postProcess.Execute(renderHelper);
        renderHelper.PopParameters();
    }

    public override void AfterRender()
    {

    }

    public void Dispose()
    {
        renderHelper?.Dispose();
        foreach (var image in images)
        {
            image.Value.Dispose();
        }
        images.Clear();
        materialSystem?.Dispose();
        rprCamera?.Dispose();
        scene?.Dispose();
        context?.Dispose();
    }

    RPRMaterialNode GetImageNode(string fileName, Rpr.MaterialNodeType nodetype = Rpr.MaterialNodeType.IMAGE_TEXTURE)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        var image = GetImage(fileName);
        if (image == null)
            return null;
        RPRMaterialNode materialNode = new RPRMaterialNode(materialSystem, nodetype);
        materialNode.SetInputImageDataByKey(Rpr.MaterialInput.DATA, image);
        return materialNode;
    }

    RPRImage GetImage(string fileName)
    {
        if (images.TryGetValue(fileName, out var image))
        {
            return image;
        }
        else
        {
            image = new RPRImage(context, fileName);
            images.Add(fileName, image);
            return image;
        }
    }
}
