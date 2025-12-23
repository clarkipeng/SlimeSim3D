using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;
using UnityEngine.UI;


[CreateAssetMenu(menuName = "Slime Settings/Display/Isosurface")]
public class IsosurfaceDisplayStrategy : DisplayStrategy
{
    protected override string KernelName => "Isosurface";

    [Header("Shader Parameters")]
    public float surfaceThreshold; // e.g., 0.1
    public float lightAmb, shinyness;
    public Vector3 lightDir;        // e.g., normalize(float3(1, 1, -1))
    public Color lightColor = Color.white;
    public Color primaryColor = Color.green;
    public Color secondaryColor = Color.blue;

    // [Range(0.5f, 5)]
    // public float alphaScale = 1.0f;
    // [LogarithmicRange(0.00001f, 1, 10)]
    // public float alphaAmb = 0.00001f;
    public Vector4 slicePlane = new Vector4(0, 0, 0, 0);

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

        shader.SetFloat("surfaceThreshold", surfaceThreshold);
        shader.SetFloat("shinyness", shinyness);
        shader.SetFloat("lightAmb", lightAmb);
        shader.SetVector("lightDir", lightDir);
        shader.SetVector("lightColor", lightColor);

        shader.SetVector("frontColor", primaryColor);
        shader.SetVector("backColor", secondaryColor);

        // shader.SetFloat("alphaScale", alphaScale);
        // shader.SetFloat("alphaAmb", alphaAmb);
        shader.SetVector("slicePlane", slicePlane);

        shader.SetTexture(kernel, "TrailMap", sourceTrailMap);
        shader.SetTexture(kernel, "Result", destinationScreen);

        ComputeHelper.Dispatch(shader, destinationScreen.width, destinationScreen.height, 1, kernelIndex: kernel);
    }
}