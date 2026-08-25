using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class MeshRenderSettings : MonoBehaviour
{
    public const uint CustomDepthRenderingLayer = 1u << 8,
        OutlineLayer = 1u << 9;

    [SerializeField]
    bool renderCustomDepth, drawOutline;
    [SerializeField, Range(0f, 0.1f)]
    float outlineWitdh = 0.1f;

    static int outlineId = Shader.PropertyToID("_OutlineWidth");
    MaterialPropertyBlock propertyBlock;

    public bool RenderCustomDepth => renderCustomDepth;
    public bool DrawOutline => drawOutline;

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void OnDisable()
    {
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            r.renderingLayerMask &= ~CustomDepthRenderingLayer;
            r.renderingLayerMask &= ~OutlineLayer;
        }
    }

    void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;

        if (renderCustomDepth)
            r.renderingLayerMask |= CustomDepthRenderingLayer;
        else
            r.renderingLayerMask &= ~CustomDepthRenderingLayer;

        if (drawOutline)
            r.renderingLayerMask |= OutlineLayer;
        else
            r.renderingLayerMask &= ~OutlineLayer;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
        r.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(outlineId, outlineWitdh);
        r.SetPropertyBlock(propertyBlock);
    }
}
