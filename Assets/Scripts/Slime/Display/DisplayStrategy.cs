using UnityEngine;
using ComputeShaderUtility;

public abstract class DisplayStrategy : ScriptableObject
{
    [Header("Shader Setup")]
    public ComputeShader shader;
    public string kernelName = "Render";

    public abstract void Dispatch(
        RenderTexture sourceTrailMap,
        RenderTexture destinationScreen,
        ComputeBuffer agentsBuffer,
        SlimeSettings settings,
        Camera camera
    );
}