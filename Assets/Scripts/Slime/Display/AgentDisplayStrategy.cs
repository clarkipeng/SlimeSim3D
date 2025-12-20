using UnityEngine;
using ComputeShaderUtility;

[CreateAssetMenu(menuName = "Slime Settings/Display/DrawAgent")]
public class AgentDisplayStrategy : DisplayStrategy
{
    [Header("Shader Parameters")]
    public Color color = Color.green;

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

        // Graphics.Blit(Texture2D.blackTexture, destinationScreen);
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
        shader.SetVector("color", color);

        ComputeHelper.Dispatch(shader, settings.numAgents, 1, 1, kernelIndex: kernel);
    }

}