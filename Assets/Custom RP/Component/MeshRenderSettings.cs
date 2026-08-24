using UnityEngine;


[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class MeshRenderSettings : MonoBehaviour
{
    public const uint CustomDepthRenderingLayer = 1u << 8;

    [SerializeField]
    bool renderCustomDepth;
    public bool RenderCustomDepth => renderCustomDepth;

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void OnDisable()
    {
        var r = GetComponent<Renderer>();
        if (r != null)
            r.renderingLayerMask &= ~CustomDepthRenderingLayer;

    }



    void Apply()
    {
        var r = GetComponent<Renderer>();
        if ((r == null)) return;


        if (renderCustomDepth)
            r.renderingLayerMask |= CustomDepthRenderingLayer;
        else
            r.renderingLayerMask &= ~CustomDepthRenderingLayer;
    }
}