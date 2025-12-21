using UnityEngine;
using ComputeShaderUtility;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

[CreateAssetMenu(menuName = "Slime Settings/Display/Agent/Direction2")]
public class AgentDirection2DisplayStrategy : DisplayStrategy
{
    protected override string KernelName => "Direction2";

    [Header("Shader Parameters")]
    public int width = 1;
    [Range(0, 5)]
    public float referenceDist = 5;

    [Header("Cosine Palette")]
    public Vector3 palA = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 palB = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 palC = new Vector3(1.0f, 1.0f, 1.0f);
    public Vector3 palD = new Vector3(0.0f, 0.33f, 0.67f);

    private RenderTexture depthBuffer;

    private void InitDepthBuffer(RenderTexture destinationScreen)
    {
        if (depthBuffer == null || depthBuffer.width != destinationScreen.width || depthBuffer.height != destinationScreen.height)
        {
            if (depthBuffer != null) depthBuffer.Release();
            depthBuffer = new RenderTexture(destinationScreen.width, destinationScreen.height, 0, GraphicsFormat.R32_SInt);
            depthBuffer.enableRandomWrite = true;
            depthBuffer.Create();
        }
    }
    public override void Dispatch(
        RenderTexture sourceTrailMap,
        RenderTexture destinationScreen,
        ComputeBuffer agentsBuffer,
        SlimeSettings settings,
        Camera camera
    )
    {
        if (shader == null) return;
        InitDepthBuffer(destinationScreen);

        int clsKernel = shader.FindKernel("ResetDepth");
        shader.SetTexture(clsKernel, "DepthBuffer", depthBuffer);
        ComputeHelper.Dispatch(shader, depthBuffer.width, depthBuffer.height, 1, kernelIndex: clsKernel);
        int kernel = shader.FindKernel(KernelName);

        var old = RenderTexture.active;
        RenderTexture.active = destinationScreen;
        Color background = Color.black;
        GL.Clear(true, true, background);
        RenderTexture.active = old;

        Matrix4x4 proj = camera.projectionMatrix;
        Matrix4x4 view = camera.worldToCameraMatrix;
        Matrix4x4 vp = proj * view;

        shader.SetInt("numAgents", settings.numAgents);
        shader.SetInt("resolution", settings.resolution);
        shader.SetInt("width", width);
        shader.SetFloat("referenceDist", referenceDist);

        shader.SetVector("palA", palA);
        shader.SetVector("palB", palB);
        shader.SetVector("palC", palC);
        shader.SetVector("palD", palD);

        shader.SetMatrix("viewProjection", vp);

        shader.SetTexture(kernel, "Result", destinationScreen);
        shader.SetBuffer(kernel, "agents", agentsBuffer);
        shader.SetTexture(kernel, "DepthBuffer", depthBuffer);

        ComputeHelper.Dispatch(shader, settings.numAgents, 1, 1, kernelIndex: kernel);
    }
    private void OnDisable()
    {
        if (depthBuffer != null) depthBuffer.Release();
    }

}