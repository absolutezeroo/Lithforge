using System.Collections.Generic;
using Lithforge.Runtime.Content.Settings;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    public sealed class TimeOfDayController : MonoBehaviour
    {
        private float _timeOfDay;
        private float _dayLengthSeconds;
        private float _sunAngleOffset;
        private float _sunAzimuth;
        private float _minSunIntensity;
        private AnimationCurve _dayNightCurve;
        private Light _directionalLight;
        private Material _voxelMaterial;
        private Material _cutoutMaterial;
        private Material _translucentMaterial;
        private readonly List<Material> _additionalMaterials = new List<Material>();

        private static readonly int s_sunLightFactorId = Shader.PropertyToID("_SunLightFactor");

        public float TimeOfDay
        {
            get { return _timeOfDay; }
        }

        public float SunLightFactor
        {
            get { return ComputeSunFactor(_timeOfDay); }
        }

        public Light DirectionalLight
        {
            get { return _directionalLight; }
        }

        public float DayLengthSeconds
        {
            get { return _dayLengthSeconds; }
            set { _dayLengthSeconds = Mathf.Max(1.0f, value); }
        }

        public void SetTimeOfDay(float time)
        {
            _timeOfDay = time % 1f;

            if (_timeOfDay < 0f)
            {
                _timeOfDay += 1f;
            }
        }

        public void Initialize(
            Material voxelMaterial,
            Material cutoutMaterial,
            Material translucentMaterial,
            RenderingSettings settings)
        {
            _voxelMaterial = voxelMaterial;
            _cutoutMaterial = cutoutMaterial;
            _translucentMaterial = translucentMaterial;
            _dayLengthSeconds = settings.DayLengthSeconds;
            _sunAngleOffset = settings.SunAngleOffset;
            _sunAzimuth = settings.SunAzimuth;
            _minSunIntensity = settings.MinSunIntensity;
            _dayNightCurve = settings.DayNightCurve;
            _timeOfDay = settings.StartTimeOfDay;

            // Find or create directional light
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    _directionalLight = lights[i];

                    break;
                }
            }
        }

        /// <summary>
        /// Registers an additional material to receive _SunLightFactor updates each frame.
        /// Used by first-person arm and held item materials.
        /// </summary>
        public void RegisterMaterial(Material material)
        {
            if (material != null && !_additionalMaterials.Contains(material))
            {
                _additionalMaterials.Add(material);
            }
        }

        private void Update()
        {
            if (_voxelMaterial == null)
            {
                return;
            }

            // Advance time
            _timeOfDay += Time.deltaTime / _dayLengthSeconds;

            if (_timeOfDay >= 1.0f)
            {
                _timeOfDay -= 1.0f;
            }

            float sunFactor = ComputeSunFactor(_timeOfDay);

            // Update materials
            _voxelMaterial.SetFloat(s_sunLightFactorId, sunFactor);

            if (_cutoutMaterial != null)
            {
                _cutoutMaterial.SetFloat(s_sunLightFactorId, sunFactor);
            }

            if (_translucentMaterial != null)
            {
                _translucentMaterial.SetFloat(s_sunLightFactorId, sunFactor);
            }

            for (int i = 0; i < _additionalMaterials.Count; i++)
            {
                _additionalMaterials[i].SetFloat(s_sunLightFactorId, sunFactor);
            }

            // Update directional light rotation (intensity is fixed; voxel light system handles brightness)
            if (_directionalLight != null)
            {
                float sunAngle = _timeOfDay * 360.0f + _sunAngleOffset;
                _directionalLight.transform.rotation = Quaternion.Euler(sunAngle, _sunAzimuth, 0.0f);
                _directionalLight.intensity = 1.0f;
            }
        }

        private float ComputeSunFactor(float time)
        {
            // Use AnimationCurve if provided
            if (_dayNightCurve != null && _dayNightCurve.length > 0)
            {
                return Mathf.Clamp(_dayNightCurve.Evaluate(time), 0f, 1f);
            }

            // Cosine fallback: 1.0 at noon (time=0.5), 0.15 at midnight (time=0.0 or 1.0)
            float cosValue = Mathf.Cos(time * 2.0f * Mathf.PI);
            float factor = 0.575f - 0.425f * cosValue;

            return Mathf.Clamp(factor, _minSunIntensity, 1.0f);
        }
    }
}
