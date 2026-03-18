using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    ///     Visual presentation: materials, sky/fog color curves, camera clipping, day/night cycle timing,
    ///     block highlight appearance, atlas tile size, and biome tint colormaps.
    /// </summary>
    /// <remarks>
    ///     Gradients and curves are sampled each frame by <c>SkyController</c> and <c>TimeOfDayController</c>
    ///     using a normalised time-of-day value (0 = midnight, 0.5 = noon, 1 = midnight).
    ///     Loaded from <c>Resources/Settings/RenderingSettings</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "RenderingSettings", menuName = "Lithforge/Settings/Rendering", order = 2)]
    public sealed class RenderingSettings : ScriptableObject
    {
        /// <summary>Material used for the opaque submesh (submesh 0) of every chunk draw call.</summary>
        [Header("Materials"), Tooltip("Opaque voxel material"), SerializeField]
         private Material opaqueMaterial;

        /// <summary>Material used for the translucent submesh (submesh 2, e.g. water) with alpha blending.</summary>
        [Tooltip("Translucent voxel material"), SerializeField]
         private Material translucentMaterial;

        /// <summary>Horizon band color keyed to normalised time of day (0=midnight, 0.5=noon).</summary>
        [Header("Sky"), Tooltip("Sky horizon color gradient over time of day (0=midnight, 0.5=noon, 1=midnight)"), SerializeField]
         private Gradient skyGradient = new();

        /// <summary>Color at the top of the sky dome, keyed to normalised time of day.</summary>
        [Tooltip("Sky zenith color gradient over time of day"), SerializeField]
         private Gradient skyZenithGradient = new();

        /// <summary>Scene ambient color keyed to normalised time of day; blends between day and night.</summary>
        [Tooltip("Ambient light gradient over time of day"), SerializeField]
         private Gradient ambientGradient = new();

        /// <summary>Fog tint keyed to normalised time of day, used with exponential-squared fog.</summary>
        [Header("Fog"), Tooltip("Fog color gradient over time of day"), SerializeField]
         private Gradient fogGradient = new();

        /// <summary>Density coefficient for exp2 fog; higher values make distant terrain fade sooner.</summary>
        [Tooltip("Fog density for exponential squared fog"), Min(0f), SerializeField]
         private float fogDensity = 0.008f;

        /// <summary>Camera near clip plane in world units; kept small to avoid clipping into nearby blocks.</summary>
        [Header("Camera"), Tooltip("Near clip plane distance"), Min(0.01f), SerializeField]
         private float nearClipPlane = 0.05f;

        /// <summary>Camera far clip plane; should exceed the render distance to avoid popping at the horizon.</summary>
        [Tooltip("Far clip plane distance"), Min(50f), SerializeField]
         private float farClipPlane = 500f;

        /// <summary>Pitch angle (degrees, positive = looking down) the camera starts at before player input.</summary>
        [Tooltip("Initial camera pitch angle in degrees at game start"), Range(-89f, 89f), SerializeField]
         private float initialCameraPitch = 30f;

        /// <summary>Real-world seconds for one complete day/night cycle (default 600 = 10 minutes).</summary>
        [Header("Day / Night Cycle"), Tooltip("Real seconds for one full day cycle"), Min(1f), SerializeField]
         private float dayLengthSeconds = 600f;

        /// <summary>Normalised time of day when the world first loads (0.5 = noon).</summary>
        [Tooltip("Starting normalised time of day (0=midnight, 0.25=sunrise, 0.5=noon)"), Range(0f, 1f), SerializeField]
         private float startTimeOfDay = 0.5f;

        /// <summary>Rotation offset (degrees) added to the sun's pitch derived from time of day.</summary>
        [Tooltip("Sun pitch rotation offset applied to time-of-day angle"), SerializeField]
         private float sunAngleOffset = -90f;

        /// <summary>Constant Y-axis rotation (degrees) of the directional light representing the sun.</summary>
        [Tooltip("Sun azimuth (Y rotation) for directional light"), SerializeField]
         private float sunAzimuth = -30f;

        /// <summary>Directional light intensity floor so the world is never completely black at night.</summary>
        [Tooltip("Minimum directional light intensity at night"), Range(0f, 1f), SerializeField]
         private float minSunIntensity = 0.1f;

        /// <summary>
        ///     Maps normalised time of day to a 0-1 sun intensity factor.
        ///     Flat plateaus at night/day with steep dawn/dusk transitions give a Minecraft-like feel.
        /// </summary>
        [Tooltip("Sun intensity curve over normalised time (0=midnight, 0.5=noon). " +
                 "If empty, falls back to cosine approximation."), SerializeField]
         private AnimationCurve dayNightCurve = new(
            new Keyframe(0.00f, 0.05f, 0f, 0f),   // midnight — night plateau start
            new Keyframe(0.18f, 0.05f, 0f, 0f),   // night plateau end
            new Keyframe(0.25f, 0.50f, 2f, 2f),   // dawn midpoint (fast rise)
            new Keyframe(0.33f, 1.00f, 0f, 0f),   // day plateau start
            new Keyframe(0.67f, 1.00f, 0f, 0f),   // day plateau end
            new Keyframe(0.75f, 0.50f, -2f, -2f), // dusk midpoint (fast fall)
            new Keyframe(0.82f, 0.05f, 0f, 0f),   // night plateau start
            new Keyframe(1.00f, 0.05f, 0f, 0f)    // midnight — wraps to 0.0
        );

        /// <summary>Ambient light intensity when the sun factor is at maximum (full daylight).</summary>
        [Header("Lighting"), Tooltip("Ambient light intensity during daytime (sunFactor=1)"), Range(0f, 1f), SerializeField]
         private float dayAmbient = 0.15f;

        /// <summary>Ambient light intensity when the sun factor is at minimum (deep night).</summary>
        [Tooltip("Ambient light intensity during nighttime (sunFactor=0)"), Range(0f, 1f), SerializeField]
         private float nightAmbient = 0.03f;

        /// <summary>Directional light color keyed to normalised time of day for warm sunrise/sunset tints.</summary>
        [Tooltip("Sun directional light color gradient over time of day (0=midnight, 0.5=noon)"), SerializeField]
         private Gradient sunColorGradient = new();

        /// <summary>Line width (world units) of the wireframe cube drawn around the targeted block.</summary>
        [Header("Block Highlight"), Tooltip("Line renderer width for block selection wireframe"), Min(0.001f), SerializeField]
         private float blockHighlightLineWidth = 0.02f;

        /// <summary>Color of the block selection wireframe lines.</summary>
        [SerializeField] private Color blockHighlightColor = Color.black;

        /// <summary>Small outward offset applied to the wireframe cube to avoid z-fighting with block faces.</summary>
        [Tooltip("Outward expansion of highlight to prevent z-fighting"), Min(0f), SerializeField]
         private float blockHighlightExpand = 0.005f;

        /// <summary>Pixel resolution of a single block face texture in the Texture2DArray atlas.</summary>
        [Header("Atlas"), Tooltip("Texture tile size in pixels for the block texture atlas"), Min(1), SerializeField]
         private int atlasTileSize = 16;

        /// <summary>256x256 Minecraft-format colormap where X=temperature and Y=humidity*temperature for grass tint.</summary>
        [Header("Biome Tinting"), Tooltip("Grass colormap (256x256 PNG, Minecraft format). X=temperature, Y=humidity*temperature"), SerializeField]
         private Texture2D grassColormap;

        /// <summary>256x256 Minecraft-format colormap for foliage tint (leaves, vines).</summary>
        [Tooltip("Foliage colormap (256x256 PNG, Minecraft format)"), SerializeField]
         private Texture2D foliageColormap;

        /// <summary>Texel dimensions of the GPU-side biome parameter texture used by BiomeTintManager.</summary>
        /// <remarks>Must be a power of 2. Larger values give finer tint transitions at the cost of VRAM.</remarks>
        [Tooltip("Size of the global biome parameter texture in texels (must be power of 2)"), Min(256), SerializeField]
         private int biomeMapSize = 2048;

        /// <inheritdoc cref="opaqueMaterial" />
        public Material OpaqueMaterial
        {
            get { return opaqueMaterial; }
        }

        /// <inheritdoc cref="translucentMaterial" />
        public Material TranslucentMaterial
        {
            get { return translucentMaterial; }
        }

        /// <inheritdoc cref="skyGradient" />
        public Gradient SkyGradient
        {
            get { return skyGradient; }
        }

        /// <inheritdoc cref="skyZenithGradient" />
        public Gradient SkyZenithGradient
        {
            get { return skyZenithGradient; }
        }

        /// <inheritdoc cref="ambientGradient" />
        public Gradient AmbientGradient
        {
            get { return ambientGradient; }
        }

        /// <inheritdoc cref="fogGradient" />
        public Gradient FogGradient
        {
            get { return fogGradient; }
        }

        /// <inheritdoc cref="fogDensity" />
        public float FogDensity
        {
            get { return fogDensity; }
        }

        /// <inheritdoc cref="nearClipPlane" />
        public float NearClipPlane
        {
            get { return nearClipPlane; }
        }

        /// <inheritdoc cref="farClipPlane" />
        public float FarClipPlane
        {
            get { return farClipPlane; }
        }

        /// <inheritdoc cref="initialCameraPitch" />
        public float InitialCameraPitch
        {
            get { return initialCameraPitch; }
        }

        /// <inheritdoc cref="dayLengthSeconds" />
        public float DayLengthSeconds
        {
            get { return dayLengthSeconds; }
        }

        /// <inheritdoc cref="startTimeOfDay" />
        public float StartTimeOfDay
        {
            get { return startTimeOfDay; }
        }

        /// <inheritdoc cref="sunAngleOffset" />
        public float SunAngleOffset
        {
            get { return sunAngleOffset; }
        }

        /// <inheritdoc cref="sunAzimuth" />
        public float SunAzimuth
        {
            get { return sunAzimuth; }
        }

        /// <inheritdoc cref="minSunIntensity" />
        public float MinSunIntensity
        {
            get { return minSunIntensity; }
        }

        /// <inheritdoc cref="dayNightCurve" />
        public AnimationCurve DayNightCurve
        {
            get { return dayNightCurve; }
        }

        /// <inheritdoc cref="blockHighlightLineWidth" />
        public float BlockHighlightLineWidth
        {
            get { return blockHighlightLineWidth; }
        }

        /// <inheritdoc cref="blockHighlightColor" />
        public Color BlockHighlightColor
        {
            get { return blockHighlightColor; }
        }

        /// <inheritdoc cref="blockHighlightExpand" />
        public float BlockHighlightExpand
        {
            get { return blockHighlightExpand; }
        }

        /// <inheritdoc cref="atlasTileSize" />
        public int AtlasTileSize
        {
            get { return atlasTileSize; }
        }

        /// <inheritdoc cref="grassColormap" />
        public Texture2D GrassColormap
        {
            get { return grassColormap; }
        }

        /// <inheritdoc cref="foliageColormap" />
        public Texture2D FoliageColormap
        {
            get { return foliageColormap; }
        }

        /// <inheritdoc cref="biomeMapSize" />
        public int BiomeMapSize
        {
            get { return biomeMapSize; }
        }

        /// <inheritdoc cref="dayAmbient" />
        public float DayAmbient
        {
            get { return dayAmbient; }
        }

        /// <inheritdoc cref="nightAmbient" />
        public float NightAmbient
        {
            get { return nightAmbient; }
        }

        /// <inheritdoc cref="sunColorGradient" />
        public Gradient SunColorGradient
        {
            get { return sunColorGradient; }
        }
    }
}
