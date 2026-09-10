using System;
using UnityEngine;


[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [Serializable]
    public struct BloomSettings
    {
        public enum Mode { Additive, Scattering }

        [Range(0f, 16f)]
        public int maxInterations;

        [Min(1f)]
        public int downscaleLimit;

        public bool bicubicUpsampling;

        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)]
        public float intensity;

        public bool fadeFireFiles;

        public Mode mode;

        [Range(0f, 1f)]
        public float scatter;
    }

    [SerializeField]
    BloomSettings bloom = default;

    public BloomSettings Bloom => bloom;


    [Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode { None,ACES, Neutral,Reinhard}
        public Mode mode;
    }

    [SerializeField]
    ToneMappingSettings tonemapping = default;

    public ToneMappingSettings ToneMapping => tonemapping;



    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float postExposure;

        [Range(-100f,100f)]
        public float contrast;

        [ColorUsage(false,true)]
        public Color colorFilter;

        [Range(-180f,180f)]
        public float hueShift;

        [Range(-100f,100f)]
        public float saturation;
    }



    [SerializeField]
    ColorAdjustmentsSettings colorAdjustments = new()
    {
        colorFilter = Color.white,
    };


    public ColorAdjustmentsSettings ColorAdjustments =>colorAdjustments;


    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f,100f)]
        public float temperature,tint;
    }

        [SerializeField]
    WhiteBalanceSettings whiteBalance = default;
    public WhiteBalanceSettings WhiteBalance => whiteBalance;



    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)]
        public Color shadows,highlights;

        [Range(-100f,100f)]
        public float balance;
    }


    [SerializeField]
    SplitToningSettings splitToning = new SplitToningSettings
    {
      shadows = Color.gray,
      highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;




    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red,green,blue;
    }

    [SerializeField]
    ChannelMixerSettings channelMixer = new ChannelMixerSettings{
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };


    public ChannelMixerSettings ChannelMixer => channelMixer;



    //阴影,中间调,高光

    [Serializable]
    public struct ShadowMidtonesHighlightsSettings
    {
        [ColorUsage(false,true)]
        public Color shadow,midtones,highlights;

        [Range(0f,2f)]
        public float shadowStart,shadowEnd,highlightsStart,highLightsEnd;
    }

    [SerializeField]
    ShadowMidtonesHighlightsSettings shadowMidtonesHightlights = new ShadowMidtonesHighlightsSettings
    {
        shadow = Color.white,
        midtones = Color.white,
        highlights = Color.white,
        shadowEnd = 0.3f,
        highlightsStart = 0.55f,
        highLightsEnd = 1f,
    };


    public  ShadowMidtonesHighlightsSettings ShadowMidtonesHightlights => shadowMidtonesHightlights;



    [SerializeField]
    Shader shader = default;
    [System.NonSerialized]
    Material material;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }
}
