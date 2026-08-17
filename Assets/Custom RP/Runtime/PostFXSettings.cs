using UnityEngine;


[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject 
{


    [System.Serializable]
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

    [System.Serializable]
    public struct ToneMappingSettings 
    {
        public enum Mode { None=-1,ACES, Neutral,Reinhard}
        public Mode mode;
    }

    [SerializeField]
    BloomSettings bloom = default;

    [SerializeField]
    ToneMappingSettings tonemapping = default;



    public BloomSettings Bloom => bloom;
    public ToneMappingSettings ToneMapping => tonemapping;


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