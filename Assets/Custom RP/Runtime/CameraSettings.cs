using System;
using UnityEngine;
using UnityEngine.Rendering;



public class RenderingLayerMaskFieldAttribute:PropertyAttribute
{

}



[Serializable]
public class CameraSettings
{
    public bool maskLights = false;


    [RenderingLayerMaskFieldAttribute]
    public int renderingLayerMask = -1;
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source,destination;
    }

    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;

    public FinalBlendMode finalBlendMode = new()
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

}




