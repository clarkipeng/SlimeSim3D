using UnityEngine;
using ComputeShaderUtility;

[CreateAssetMenu(menuName = "Slime Settings/Display/Default")]
public class DefaultDisplayStrategy : DisplayStrategy
{
    [Header("Shader Parameters")]
    public Color color = Color.green;

    public override void Dispatch(
        RenderTexture sourceTrailMap,
        RenderTexture destinationScreen,
        int resolution,
        Camera camera
    )
    {
        if (shader == null) return;
        int kernel = shader.FindKernel(kernelName);

        shader.SetInt("resolution", resolution);
        shader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
        shader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);

        shader.SetVector("color", color);

        shader.SetTexture(kernel, "TrailMap", sourceTrailMap);
        shader.SetTexture(kernel, "Result", destinationScreen);

        ComputeHelper.Dispatch(shader, destinationScreen.width, destinationScreen.height, 1, kernelIndex: kernel);
    }
}