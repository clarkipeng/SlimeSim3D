using UnityEngine;

[CreateAssetMenu(fileName = "NewShot", menuName = "Cinematics/Shot")]
public class KeyFrame : ScriptableObject
{
    public enum EasingType { Linear, SmoothStep, SmootherStep, EaseInQuad, EaseOutQuad, EaseInCubic, EaseOutCubic }

    [Header("Timing")]
    public float duration = 5.0f;
    public float holdTime = 0.0f;
    public EasingType easing = EasingType.SmootherStep;

    [Header("Orbit Camera")]
    public float yaw;
    public float pitch;
    public float radius;

    [Header("Visuals")]
    public DisplayStrategy displaySettings;
}