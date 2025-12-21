using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;
using UnityEngine.UI;


[CreateAssetMenu(menuName = "Slime Settings/Display/Density")]
public class DensityDisplayStrategy : DisplayStrategy
{
    [Header("Shader Parameters")]
    public Color colorLow = Color.blue;
    public Color colorMid = Color.red;
    public Color colorHigh = Color.white;

    [Range(0.5f, 5)]
    public float alphaScale = 1.0f;
    [LogarithmicRange(0.00001f, 1, 10)]
    public float alphaAmb = 0.00001f;

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

        shader.SetInt("resolution", settings.resolution);
        shader.SetInt("boundaryRadius", settings.boundaryRadius);

        shader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
        shader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);

        shader.SetVector("colorLow", colorLow);
        shader.SetVector("colorMid", colorMid);
        shader.SetVector("colorHigh", colorHigh);

        shader.SetFloat("alphaScale", alphaScale);
        shader.SetFloat("alphaAmb", alphaAmb);

        shader.SetTexture(kernel, "TrailMap", sourceTrailMap);
        shader.SetTexture(kernel, "Result", destinationScreen);

        ComputeHelper.Dispatch(shader, destinationScreen.width, destinationScreen.height, 1, kernelIndex: kernel);
    }
}