using UnityEngine;
using UnityEditor;



[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        if (
            !settings.lightType.hasMultipleDifferentValues && //确保没有选择多种类灯光
            (LightType)settings.lightType.enumValueIndex == LightType.Spot//保证选择的是聚光灯
            )
        {
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }

        base.OnInspectorGUI();

    }
}