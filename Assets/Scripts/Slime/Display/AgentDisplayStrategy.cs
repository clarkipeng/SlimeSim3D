using UnityEngine;
using ComputeShaderUtility;

[CreateAssetMenu(menuName = "Slime Settings/Display/Agent/Simple")]
public class AgentDisplayStrategy : DisplayStrategy
{
    protected override string KernelName => "Simple";

    [Header("Shader Parameters")]
    public Color color = Color.green;
    public int width = 1;

    public override void Dispatch(
        RenderTexture sourceTrailMap,
        RenderTexture destinationScreen,
        ComputeBuffer agentsBuffer,
        SlimeSettings settings,
        Camera camera
    )
    {
        if (shader == null) return;
        int kernel = shader.FindKernel(KernelName);

        var old = RenderTexture.active;
        RenderTexture.active = destinationScreen;
        Color background = Color.black;
        background.a = 0.0f;
        GL.Clear(true, true, background);
        RenderTexture.active = old;

        Matrix4x4 proj = camera.projectionMatrix;
        Matrix4x4 view = camera.worldToCameraMatrix;
        Matrix4x4 vp = proj * view;

        shader.SetInt("resolution", settings.resolution);

        shader.SetTexture(kernel, "Result", destinationScreen);
        shader.SetBuffer(kernel, "agents", agentsBuffer);
        shader.SetInt("numAgents", settings.numAgents);

        shader.SetMatrix("viewProjection", vp);
        shader.SetVector("color", color);
        shader.SetInt("width", width);

        ComputeHelper.Dispatch(shader, settings.numAgents, 1, 1, kernelIndex: kernel);
    }

}