// Assets/Scripts/Slime/Editor/LogarithmicRangeDrawer.cs
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(LogarithmicRangeAttribute))]
public class LogarithmicRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        LogarithmicRangeAttribute attribute = (LogarithmicRangeAttribute)this.attribute;
        if (property.propertyType != SerializedPropertyType.Float)
        {
            EditorGUI.LabelField(position, label.text, "Use LogarithmicRange with float.");
            return;
        }

        Slider(position, property, attribute.min, attribute.max, attribute.power, label);
    }

    public static void Slider(
        Rect position, SerializedProperty property,
        float leftValue, float rightValue, float power, GUIContent label)
    {
        label = EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        float num = PowerSlider(position, label, property.floatValue, leftValue, rightValue, power);

        if (EditorGUI.EndChangeCheck())
            property.floatValue = num;
        EditorGUI.EndProperty();
    }

    public static float PowerSlider(Rect position, GUIContent label, float value, float leftValue, float rightValue, float power)
    {
        var editorGuiType = typeof(EditorGUI);
        var methodInfo = editorGuiType.GetMethod(
            "PowerSlider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(Rect), typeof(GUIContent), typeof(float), typeof(float), typeof(float), typeof(float) },
            null);
        if (methodInfo != null)
        {
            return (float)methodInfo.Invoke(null, new object[] { position, label, value, leftValue, rightValue, power });
        }
        return leftValue;
    }
}