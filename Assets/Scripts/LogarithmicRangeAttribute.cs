using UnityEngine;
// [AttributeUsage(AttributeTargets.Field)]
public class LogarithmicRangeAttribute : PropertyAttribute
{
    public readonly float min = 1e-3f;
    public readonly float max = 1e3f;
    public readonly float power = 2;
    public LogarithmicRangeAttribute(float min, float max, float power)
    {
        if (min <= 0)
        {
            min = 1e-4f;
        }
        this.min = min;
        this.max = max;
        this.power = power;
    }
}