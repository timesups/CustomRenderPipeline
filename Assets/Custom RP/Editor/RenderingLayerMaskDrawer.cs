using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    public static void Draw(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginProperty(position, label, property);

        int mask = property.intValue;
        bool isUint = property.type == "uint";
        if (mask == int.MaxValue)
        {
            mask = -1;
        }

        EditorGUI.BeginChangeCheck();
        mask = EditorGUI.MaskField(position, label, mask, GetRenderingLayerNames());
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }

        EditorGUI.EndProperty();
        EditorGUI.showMixedValue = false;
    }

    public static void Draw(SerializedProperty property, GUIContent label)
    {
        Draw(EditorGUILayout.GetControlRect(), property, label);
    }

    public override void OnGUI(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        Draw(position, property, label);
    }

    static string[] GetRenderingLayerNames()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null)
        {
            string[] names = pipeline.renderingLayerMaskNames;
            if (names != null && names.Length > 0)
            {
                return names;
            }
        }

        var fallback = new string[31];
        for (int i = 0; i < fallback.Length; i++)
        {
            fallback[i] = "Layer " + (i + 1);
        }
        return fallback;
    }
}
