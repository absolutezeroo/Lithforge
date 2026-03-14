using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "RenderingSettings", menuName = "Lithforge/Settings/Rendering", order = 2)]
    public sealed class RenderingSettings : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Opaque voxel material")]
        [FormerlySerializedAs("opaqueMaterial")]
        [SerializeField] private Material _opaqueMaterial;

        [Tooltip("Translucent voxel material")]
        [FormerlySerializedAs("translucentMaterial")]
        [SerializeField] private Material _translucentMaterial;

        [Header("Sky")]
        [Tooltip("Sky horizon color gradient over time of day (0=midnight, 0.5=noon, 1=midnight)")]
        [FormerlySerializedAs("skyGradient")]
        [SerializeField] private Gradient _skyGradient = new Gradient();

        [Tooltip("Sky zenith color gradient over time of day")]
        [FormerlySerializedAs("skyZenithGradient")]
        [SerializeField] private Gradient _skyZenithGradient = new Gradient();

        [Tooltip("Ambient light gradient over time of day")]
        [FormerlySerializedAs("ambientGradient")]
        [SerializeField] private Gradient _ambientGradient = new Gradient();

        [Header("Fog")]
        [Tooltip("Fog color gradient over time of day")]
        [FormerlySerializedAs("fogGradient")]
        [SerializeField] private Gradient _fogGradient = new Gradient();

        [Tooltip("Fog density for exponential squared fog")]
        [Min(0f)]
        [FormerlySerializedAs("fogDensity")]
        [SerializeField] private float _fogDensity = 0.008f;

        [Header("Camera")]
        [Tooltip("Near clip plane distance")]
        [Min(0.01f)]
        [FormerlySerializedAs("nearClipPlane")]
        [SerializeField] private float _nearClipPlane = 0.05f;

        [Tooltip("Far clip plane distance")]
        [Min(50f)]
        [FormerlySerializedAs("farClipPlane")]
        [SerializeField] private float _farClipPlane = 500f;

        [Tooltip("Initial camera pitch angle in degrees at game start")]
        [Range(-89f, 89f)]
        [FormerlySerializedAs("initialCameraPitch")]
        [SerializeField] private float _initialCameraPitch = 30f;

        [Header("Day / Night Cycle")]
        [Tooltip("Real seconds for one full day cycle")]
        [Min(1f)]
        [FormerlySerializedAs("dayLengthSeconds")]
        [SerializeField] private float _dayLengthSeconds = 600f;

        [Tooltip("Starting normalised time of day (0=midnight, 0.25=sunrise, 0.5=noon)")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("startTimeOfDay")]
        [SerializeField] private float _startTimeOfDay = 0.5f;

        [Tooltip("Sun pitch rotation offset applied to time-of-day angle")]
        [FormerlySerializedAs("sunAngleOffset")]
        [SerializeField] private float _sunAngleOffset = -90f;

        [Tooltip("Sun azimuth (Y rotation) for directional light")]
        [FormerlySerializedAs("sunAzimuth")]
        [SerializeField] private float _sunAzimuth = -30f;

        [Tooltip("Minimum directional light intensity at night")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("minSunIntensity")]
        [SerializeField] private float _minSunIntensity = 0.1f;

        [Tooltip("Sun intensity curve over normalised time (0=midnight, 0.5=noon). " +
                 "If empty, falls back to cosine approximation.")]
        [FormerlySerializedAs("dayNightCurve")]
        [SerializeField] private AnimationCurve _dayNightCurve = new AnimationCurve();

        [Header("Block Highlight")]
        [Tooltip("Line renderer width for block selection wireframe")]
        [Min(0.001f)]
        [FormerlySerializedAs("blockHighlightLineWidth")]
        [SerializeField] private float _blockHighlightLineWidth = 0.02f;

        [FormerlySerializedAs("blockHighlightColor")]
        [SerializeField] private Color _blockHighlightColor = Color.black;

        [Tooltip("Outward expansion of highlight to prevent z-fighting")]
        [Min(0f)]
        [FormerlySerializedAs("blockHighlightExpand")]
        [SerializeField] private float _blockHighlightExpand = 0.005f;

        [Header("Atlas")]
        [Tooltip("Texture tile size in pixels for the block texture atlas")]
        [Min(1)]
        [FormerlySerializedAs("atlasTileSize")]
        [SerializeField] private int _atlasTileSize = 16;

        [Header("Biome Tinting")]
        [Tooltip("Grass colormap (256x256 PNG, Minecraft format). X=temperature, Y=humidity*temperature")]
        [FormerlySerializedAs("grassColormap")]
        [SerializeField] private Texture2D _grassColormap;

        [Tooltip("Foliage colormap (256x256 PNG, Minecraft format)")]
        [FormerlySerializedAs("foliageColormap")]
        [SerializeField] private Texture2D _foliageColormap;

        [Tooltip("Size of the global biome parameter texture in texels (must be power of 2)")]
        [Min(256)]
        [FormerlySerializedAs("biomeMapSize")]
        [SerializeField] private int _biomeMapSize = 2048;

        public Material OpaqueMaterial
        {
            get { return _opaqueMaterial; }
        }

        public Material TranslucentMaterial
        {
            get { return _translucentMaterial; }
        }

        public Gradient SkyGradient
        {
            get { return _skyGradient; }
        }

        public Gradient SkyZenithGradient
        {
            get { return _skyZenithGradient; }
        }

        public Gradient AmbientGradient
        {
            get { return _ambientGradient; }
        }

        public Gradient FogGradient
        {
            get { return _fogGradient; }
        }

        public float FogDensity
        {
            get { return _fogDensity; }
        }

        public float NearClipPlane
        {
            get { return _nearClipPlane; }
        }

        public float FarClipPlane
        {
            get { return _farClipPlane; }
        }

        public float InitialCameraPitch
        {
            get { return _initialCameraPitch; }
        }

        public float DayLengthSeconds
        {
            get { return _dayLengthSeconds; }
        }

        public float StartTimeOfDay
        {
            get { return _startTimeOfDay; }
        }

        public float SunAngleOffset
        {
            get { return _sunAngleOffset; }
        }

        public float SunAzimuth
        {
            get { return _sunAzimuth; }
        }

        public float MinSunIntensity
        {
            get { return _minSunIntensity; }
        }

        public AnimationCurve DayNightCurve
        {
            get { return _dayNightCurve; }
        }

        public float BlockHighlightLineWidth
        {
            get { return _blockHighlightLineWidth; }
        }

        public Color BlockHighlightColor
        {
            get { return _blockHighlightColor; }
        }

        public float BlockHighlightExpand
        {
            get { return _blockHighlightExpand; }
        }

        public int AtlasTileSize
        {
            get { return _atlasTileSize; }
        }

        public Texture2D GrassColormap
        {
            get { return _grassColormap; }
        }

        public Texture2D FoliageColormap
        {
            get { return _foliageColormap; }
        }

        public int BiomeMapSize
        {
            get { return _biomeMapSize; }
        }
    }
}
