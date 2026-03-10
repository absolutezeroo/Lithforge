using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "RenderingSettings", menuName = "Lithforge/Settings/Rendering", order = 2)]
    public sealed class RenderingSettings : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Opaque voxel material")]
        [SerializeField] private Material _opaqueMaterial;

        [Tooltip("Translucent voxel material")]
        [SerializeField] private Material _translucentMaterial;

        [Header("Sky")]
        [Tooltip("Sky color gradient over time of day (0=midnight, 0.5=noon, 1=midnight)")]
        [SerializeField] private Gradient _skyGradient;

        [Tooltip("Ambient light gradient over time of day")]
        [SerializeField] private Gradient _ambientGradient;

        [Header("Camera")]
        [Tooltip("Far clip plane distance")]
        [Min(50f)]
        [SerializeField] private float _farClipPlane = 500f;

        [Tooltip("Initial camera pitch angle in degrees at game start")]
        [Range(-89f, 89f)]
        [SerializeField] private float _initialCameraPitch = 30f;

        [Header("Day / Night Cycle")]
        [Tooltip("Real seconds for one full day cycle")]
        [Min(1f)]
        [SerializeField] private float _dayLengthSeconds = 600f;

        [Tooltip("Starting normalised time of day (0=midnight, 0.25=sunrise, 0.5=noon)")]
        [Range(0f, 1f)]
        [SerializeField] private float _startTimeOfDay = 0.25f;

        [Tooltip("Sun pitch rotation offset applied to time-of-day angle")]
        [SerializeField] private float _sunAngleOffset = -90f;

        [Tooltip("Sun azimuth (Y rotation) for directional light")]
        [SerializeField] private float _sunAzimuth = -30f;

        [Tooltip("Minimum directional light intensity at night")]
        [Range(0f, 1f)]
        [SerializeField] private float _minSunIntensity = 0.1f;

        [Tooltip("Sun intensity curve over normalised time (0=midnight, 0.5=noon). " +
                 "If empty, falls back to cosine approximation.")]
        [SerializeField] private AnimationCurve _dayNightCurve;

        [Header("Block Highlight")]
        [Tooltip("Line renderer width for block selection wireframe")]
        [Min(0.001f)]
        [SerializeField] private float _blockHighlightLineWidth = 0.02f;

        [SerializeField] private Color _blockHighlightColor = Color.black;

        [Tooltip("Outward expansion of highlight to prevent z-fighting")]
        [Min(0f)]
        [SerializeField] private float _blockHighlightExpand = 0.005f;

        [Header("Atlas")]
        [Tooltip("Texture tile size in pixels for the block texture atlas")]
        [Min(1)]
        [SerializeField] private int _atlasTileSize = 16;

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

        public Gradient AmbientGradient
        {
            get { return _ambientGradient; }
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
    }
}
