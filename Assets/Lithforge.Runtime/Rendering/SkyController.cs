using Lithforge.Runtime.Content.Settings;
using Lithforge.Voxel.Chunk;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Drives the procedural skybox material, fog color, and ambient lighting
    ///     based on the current time of day. Reads TimeOfDayController each frame
    ///     and evaluates RenderingSettings gradients to produce smooth day/night transitions.
    /// </summary>
    public sealed class SkyController : MonoBehaviour
    {
        /// <summary>Interval in seconds between DynamicGI.UpdateEnvironment() calls.</summary>
        private const float DynamicGIInterval = 2.0f;

        /// <summary>Shader property ID for the horizon sky color.</summary>
        private static readonly int s_horizonColorId = Shader.PropertyToID("_HorizonColor");

        /// <summary>Shader property ID for the zenith sky color.</summary>
        private static readonly int s_zenithColorId = Shader.PropertyToID("_ZenithColor");

        /// <summary>Shader property ID for the sun direction vector.</summary>
        private static readonly int s_sunDirectionId = Shader.PropertyToID("_SunDirection");

        /// <summary>Shader property ID for star field visibility (0 = hidden, 1 = full).</summary>
        private static readonly int s_starVisibilityId = Shader.PropertyToID("_StarVisibility");

        /// <summary>Gradient mapping time-of-day to ambient light color.</summary>
        private Gradient _ambientGradient;

        /// <summary>Logger for diagnostic messages.</summary>
        private ILogger _logger;

        /// <summary>Base fog density before render-distance scaling is applied.</summary>
        private float _baseFogDensity;

        /// <summary>Chunk manager reference for render distance based fog scaling.</summary>
        private ChunkManager _chunkManager;

        /// <summary>Scene directional light driven by the sun rotation.</summary>
        private Light _directionalLight;

        /// <summary>Accumulator for throttling DynamicGI environment updates.</summary>
        private float _dynamicGITimer;

        /// <summary>Gradient mapping time-of-day to fog color.</summary>
        private Gradient _fogGradient;

        /// <summary>Dynamically created procedural skybox material instance.</summary>
        private Material _skyboxMaterial;

        /// <summary>Gradient mapping time-of-day to horizon sky color.</summary>
        private Gradient _skyGradient;

        /// <summary>Gradient mapping time-of-day to zenith sky color.</summary>
        private Gradient _skyZenithGradient;

        /// <summary>Gradient mapping time-of-day to directional light color.</summary>
        private Gradient _sunColorGradient;

        /// <summary>Time-of-day controller providing the current normalized day cycle value.</summary>
        private TimeOfDayController _timeOfDayController;

        /// <summary>Evaluates time-of-day gradients and applies sky, fog, ambient, and light updates.</summary>
        private void Update()
        {
            if (_timeOfDayController == null || _skyboxMaterial == null)
            {
                return;
            }

            float time = _timeOfDayController.TimeOfDay;
            float sunFactor = _timeOfDayController.SunLightFactor;

            Color horizonColor = _skyGradient.Evaluate(time);
            Color zenithColor = _skyZenithGradient.Evaluate(time);
            Color fogColor = _fogGradient.Evaluate(time);
            Color ambientColor = _ambientGradient.Evaluate(time);

            _skyboxMaterial.SetColor(s_horizonColorId, horizonColor);
            _skyboxMaterial.SetColor(s_zenithColorId, zenithColor);
            _skyboxMaterial.SetFloat(s_starVisibilityId, 1.0f - sunFactor);

            if (_directionalLight != null)
            {
                _skyboxMaterial.SetVector(s_sunDirectionId,
                    -_directionalLight.transform.forward);

                // Apply sun color gradient to directional light
                if (_sunColorGradient != null && _sunColorGradient.colorKeys.Length > 1)
                {
                    _directionalLight.color = _sunColorGradient.Evaluate(time);
                }

                // Warm fog tint when sun is near horizon (sunrise/sunset)
                float sunElevation = _directionalLight.transform.forward.y;
                float horizonFactor = 1f - Mathf.Clamp01(Mathf.Abs(sunElevation) * 3f);
                Color warmTint = new(1f, 0.65f, 0.35f);
                fogColor = Color.Lerp(fogColor, warmTint, horizonFactor * 0.4f * sunFactor);
            }

            // Scale fog density inversely with render distance so distant LOD chunks
            // are not fully obscured by fog
            if (_chunkManager != null)
            {
                int renderDistance = _chunkManager.RenderDistance;
                float scaledDensity = _baseFogDensity * (8.0f / Mathf.Max(renderDistance, 1));
                RenderSettings.fogDensity = scaledDensity;
            }

            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientSkyColor = ambientColor;
            RenderSettings.ambientEquatorColor = Color.Lerp(ambientColor, horizonColor, 0.5f);
            RenderSettings.ambientGroundColor = ambientColor * 0.3f;

            _dynamicGITimer += Time.deltaTime;

            if (_dynamicGITimer >= DynamicGIInterval)
            {
                _dynamicGITimer = 0.0f;
                DynamicGI.UpdateEnvironment();
            }
        }

        /// <summary>Destroys the dynamically created skybox material instance.</summary>
        private void OnDestroy()
        {
            if (_skyboxMaterial != null)
            {
                Destroy(_skyboxMaterial);
            }
        }

        /// <summary>Initializes the sky controller with gradients, fog settings, and creates the skybox material.</summary>
        public void Initialize(
            TimeOfDayController timeOfDayController,
            Light directionalLight,
            RenderingSettings settings,
            ChunkManager chunkManager = null,
            ILogger logger = null)
        {
            _timeOfDayController = timeOfDayController;
            _directionalLight = directionalLight;
            _chunkManager = chunkManager;
            _logger = logger;
            _skyGradient = settings.SkyGradient;
            _skyZenithGradient = settings.SkyZenithGradient;
            _fogGradient = settings.FogGradient;
            _ambientGradient = settings.AmbientGradient;
            _sunColorGradient = settings.SunColorGradient;
            _baseFogDensity = settings.FogDensity;

            Shader skyShader = Shader.Find("Lithforge/ProceduralSky");

            if (skyShader == null)
            {
                _logger?.LogError("[Lithforge] ProceduralSky shader not found.");
                return;
            }

            _skyboxMaterial = new Material(skyShader);
            RenderSettings.skybox = _skyboxMaterial;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _baseFogDensity;
            RenderSettings.fog = true;
        }
    }
}
