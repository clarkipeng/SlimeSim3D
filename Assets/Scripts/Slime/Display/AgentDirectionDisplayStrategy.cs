using UnityEngine;
using ComputeShaderUtility;

[CreateAssetMenu(menuName = "Slime Settings/Display/AgentDirection")]
public class AgentDirectionDisplayStrategy : DisplayStrategy
{
    [Header("Shader Parameters")]
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
        int kernel = shader.FindKernel(kernelName);

        var old = RenderTexture.active;
        RenderTexture.active = destinationScreen;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = old;

        Matrix4x4 proj = camera.projectionMatrix;
        Matrix4x4 view = camera.worldToCameraMatrix;
        Matrix4x4 vp = proj * view;

        shader.SetInt("resolution", settings.resolution);

        shader.SetTexture(kernel, "Result", destinationScreen);
        shader.SetBuffer(kernel, "agents", agentsBuffer);
        shader.SetInt("numAgents", settings.numAgents);

        shader.SetMatrix("viewProjection", vp);
        shader.SetInt("width", width);

        ComputeHelper.Dispatch(shader, settings.numAgents, 1, 1, kernelIndex: kernel);

        int alphaKernel = shader.FindKernel("ResetAlpha");
        shader.SetTexture(alphaKernel, "Result", destinationScreen);
        ComputeHelper.Dispatch(shader, destinationScreen.width, destinationScreen.height, 1, kernelIndex: alphaKernel);
    }

}