using Lithforge.Runtime.Content.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Drives the procedural skybox material, fog color, and ambient lighting
    /// based on the current time of day. Reads TimeOfDayController each frame
    /// and evaluates RenderingSettings gradients to produce smooth day/night transitions.
    /// </summary>
    public sealed class SkyController : MonoBehaviour
    {
        private TimeOfDayController _timeOfDayController;
        private Light _directionalLight;
        private Material _skyboxMaterial;
        private Gradient _skyGradient;
        private Gradient _skyZenithGradient;
        private Gradient _fogGradient;
        private Gradient _ambientGradient;
        private float _fogDensity;

        private float _dynamicGITimer;
        private const float _dynamicGIInterval = 2.0f;

        private static readonly int _horizonColorId = Shader.PropertyToID("_HorizonColor");
        private static readonly int _zenithColorId = Shader.PropertyToID("_ZenithColor");
        private static readonly int _sunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int _starVisibilityId = Shader.PropertyToID("_StarVisibility");

        public void Initialize(
            TimeOfDayController timeOfDayController,
            Light directionalLight,
            RenderingSettings settings)
        {
            _timeOfDayController = timeOfDayController;
            _directionalLight = directionalLight;
            _skyGradient = settings.SkyGradient;
            _skyZenithGradient = settings.SkyZenithGradient;
            _fogGradient = settings.FogGradient;
            _ambientGradient = settings.AmbientGradient;
            _fogDensity = settings.FogDensity;

            Shader skyShader = Shader.Find("Lithforge/ProceduralSky");

            if (skyShader == null)
            {
                UnityEngine.Debug.LogError("[Lithforge] ProceduralSky shader not found.");
                return;
            }

            _skyboxMaterial = new Material(skyShader);
            RenderSettings.skybox = _skyboxMaterial;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fog = true;
        }

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

            _skyboxMaterial.SetColor(_horizonColorId, horizonColor);
            _skyboxMaterial.SetColor(_zenithColorId, zenithColor);
            _skyboxMaterial.SetFloat(_starVisibilityId, 1.0f - sunFactor);

            if (_directionalLight != null)
            {
                _skyboxMaterial.SetVector(_sunDirectionId,
                    -_directionalLight.transform.forward);
            }

            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientSkyColor = ambientColor;
            RenderSettings.ambientEquatorColor = Color.Lerp(ambientColor, horizonColor, 0.5f);
            RenderSettings.ambientGroundColor = ambientColor * 0.3f;

            _dynamicGITimer += Time.deltaTime;

            if (_dynamicGITimer >= _dynamicGIInterval)
            {
                _dynamicGITimer = 0.0f;
                DynamicGI.UpdateEnvironment();
            }
        }

        private void OnDestroy()
        {
            if (_skyboxMaterial != null)
            {
                Destroy(_skyboxMaterial);
            }
        }
    }
}
