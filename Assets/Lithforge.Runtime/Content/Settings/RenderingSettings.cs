using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "RenderingSettings", menuName = "Lithforge/Settings/Rendering", order = 2)]
    public sealed class RenderingSettings : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Opaque voxel material")]
        [SerializeField] private Material opaqueMaterial;

        [Tooltip("Translucent voxel material")]
        [SerializeField] private Material translucentMaterial;

        [Header("Sky")]
        [Tooltip("Sky horizon color gradient over time of day (0=midnight, 0.5=noon, 1=midnight)")]
        [SerializeField] private Gradient skyGradient = new Gradient();

        [Tooltip("Sky zenith color gradient over time of day")]
        [SerializeField] private Gradient skyZenithGradient = new Gradient();

        [Tooltip("Ambient light gradient over time of day")]
        [SerializeField] private Gradient ambientGradient = new Gradient();

        [Header("Fog")]
        [Tooltip("Fog color gradient over time of day")]
        [SerializeField] private Gradient fogGradient = new Gradient();

        [Tooltip("Fog density for exponential squared fog")]
        [Min(0f)]
        [SerializeField] private float fogDensity = 0.008f;

        [Header("Camera")]
        [Tooltip("Near clip plane distance")]
        [Min(0.01f)]
        [SerializeField] private float nearClipPlane = 0.05f;

        [Tooltip("Far clip plane distance")]
        [Min(50f)]
        [SerializeField] private float farClipPlane = 500f;

        [Tooltip("Initial camera pitch angle in degrees at game start")]
        [Range(-89f, 89f)]
        [SerializeField] private float initialCameraPitch = 30f;

        [Header("Day / Night Cycle")]
        [Tooltip("Real seconds for one full day cycle")]
        [Min(1f)]
        [SerializeField] private float dayLengthSeconds = 600f;

        [Tooltip("Starting normalised time of day (0=midnight, 0.25=sunrise, 0.5=noon)")]
        [Range(0f, 1f)]
        [SerializeField] private float startTimeOfDay = 0.5f;

        [Tooltip("Sun pitch rotation offset applied to time-of-day angle")]
        [SerializeField] private float sunAngleOffset = -90f;

        [Tooltip("Sun azimuth (Y rotation) for directional light")]
        [SerializeField] private float sunAzimuth = -30f;

        [Tooltip("Minimum directional light intensity at night")]
        [Range(0f, 1f)]
        [SerializeField] private float minSunIntensity = 0.1f;

        [Tooltip("Sun intensity curve over normalised time (0=midnight, 0.5=noon). " +
                 "If empty, falls back to cosine approximation.")]
        [SerializeField] private AnimationCurve dayNightCurve = new AnimationCurve();

        [Header("Block Highlight")]
        [Tooltip("Line renderer width for block selection wireframe")]
        [Min(0.001f)]
        [SerializeField] private float blockHighlightLineWidth = 0.02f;

        [SerializeField] private Color blockHighlightColor = Color.black;

        [Tooltip("Outward expansion of highlight to prevent z-fighting")]
        [Min(0f)]
        [SerializeField] private float blockHighlightExpand = 0.005f;

        [Header("Atlas")]
        [Tooltip("Texture tile size in pixels for the block texture atlas")]
        [Min(1)]
        [SerializeField] private int atlasTileSize = 16;

        [Header("Biome Tinting")]
        [Tooltip("Grass colormap (256x256 PNG, Minecraft format). X=temperature, Y=humidity*temperature")]
        [SerializeField] private Texture2D grassColormap;

        [Tooltip("Foliage colormap (256x256 PNG, Minecraft format)")]
        [SerializeField] private Texture2D foliageColormap;

        [Tooltip("Size of the global biome parameter texture in texels (must be power of 2)")]
        [Min(256)]
        [SerializeField] private int biomeMapSize = 2048;

        public Material OpaqueMaterial
        {
            get { return opaqueMaterial; }
        }

        public Material TranslucentMaterial
        {
            get { return translucentMaterial; }
        }

        public Gradient SkyGradient
        {
            get { return skyGradient; }
        }

        public Gradient SkyZenithGradient
        {
            get { return skyZenithGradient; }
        }

        public Gradient AmbientGradient
        {
            get { return ambientGradient; }
        }

        public Gradient FogGradient
        {
            get { return fogGradient; }
        }

        public float FogDensity
        {
            get { return fogDensity; }
        }

        public float NearClipPlane
        {
            get { return nearClipPlane; }
        }

        public float FarClipPlane
        {
            get { return farClipPlane; }
        }

        public float InitialCameraPitch
        {
            get { return initialCameraPitch; }
        }

        public float DayLengthSeconds
        {
            get { return dayLengthSeconds; }
        }

        public float StartTimeOfDay
        {
            get { return startTimeOfDay; }
        }

        public float SunAngleOffset
        {
            get { return sunAngleOffset; }
        }

        public float SunAzimuth
        {
            get { return sunAzimuth; }
        }

        public float MinSunIntensity
        {
            get { return minSunIntensity; }
        }

        public AnimationCurve DayNightCurve
        {
            get { return dayNightCurve; }
        }

        public float BlockHighlightLineWidth
        {
            get { return blockHighlightLineWidth; }
        }

        public Color BlockHighlightColor
        {
            get { return blockHighlightColor; }
        }

        public float BlockHighlightExpand
        {
            get { return blockHighlightExpand; }
        }

        public int AtlasTileSize
        {
            get { return atlasTileSize; }
        }

        public Texture2D GrassColormap
        {
            get { return grassColormap; }
        }

        public Texture2D FoliageColormap
        {
            get { return foliageColormap; }
        }

        public int BiomeMapSize
        {
            get { return biomeMapSize; }
        }
    }
}
