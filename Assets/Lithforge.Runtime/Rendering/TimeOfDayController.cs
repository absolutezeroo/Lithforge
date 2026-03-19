using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;

using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Advances the day/night cycle and pushes the resulting sun light factor,
    ///     ambient light, and directional light rotation to all voxel materials each frame.
    ///     Time advances at fixed tick rate via AdvanceTick; visual updates run at frame rate.
    ///     Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class TimeOfDayController : MonoBehaviour
    {
        /// <summary>Shader property ID for the sun light brightness factor.</summary>
        private static readonly int s_sunLightFactorId = Shader.PropertyToID("_SunLightFactor");

        /// <summary>Shader property ID for the ambient light level.</summary>
        private static readonly int s_ambientLightId = Shader.PropertyToID("_AmbientLight");

        /// <summary>Additional materials registered for sun/ambient updates (e.g., held item materials).</summary>
        private readonly List<Material> _additionalMaterials = new();

        /// <summary>Cutout (alpha-test) voxel material receiving sun/ambient updates.</summary>
        private Material _cutoutMaterial;

        /// <summary>Ambient light level during full daylight.</summary>
        private float _dayAmbient;

        /// <summary>Real-time seconds for one full day/night cycle.</summary>
        private float _dayLengthSeconds;

        /// <summary>AnimationCurve mapping time-of-day [0,1] to sun factor [0,1], or null for cosine fallback.</summary>
        private AnimationCurve _dayNightCurve;

        /// <summary>Minimum sun intensity (floor) to prevent total darkness at midnight.</summary>
        private float _minSunIntensity;

        /// <summary>Ambient light level during nighttime.</summary>
        private float _nightAmbient;

        /// <summary>Rotational offset in degrees applied to the sun's elevation angle.</summary>
        private float _sunAngleOffset;

        /// <summary>Azimuth angle in degrees for the directional light's Y rotation.</summary>
        private float _sunAzimuth;

        /// <summary>Translucent (water) voxel material receiving sun/ambient updates.</summary>
        private Material _translucentMaterial;

        /// <summary>Opaque voxel material receiving sun/ambient updates.</summary>
        private Material _voxelMaterial;

        /// <summary>Normalized time of day in [0,1). 0 = midnight, 0.5 = noon.</summary>
        public float TimeOfDay { get; private set; }

        /// <summary>Current brightness multiplier in [0,1] derived from the day/night curve.</summary>
        public float SunLightFactor
        {
            get { return ComputeSunFactor(TimeOfDay); }
        }

        /// <summary>The scene directional light rotated to match the sun's position.</summary>
        public Light DirectionalLight { get; private set; }

        /// <summary>Gets or sets the real-time length of one full day/night cycle in seconds (minimum 1).</summary>
        public float DayLengthSeconds
        {
            get { return _dayLengthSeconds; }
            set { _dayLengthSeconds = Mathf.Max(1.0f, value); }
        }

        /// <summary>Applies visual updates to materials and directional light rotation at frame rate.</summary>
        private void Update()
        {
            if (_voxelMaterial == null)
            {
                return;
            }

            // Time advancement now happens in AdvanceTick() at fixed tick rate.
            // This Update() only applies visual changes (materials, light rotation).

            float sunFactor = ComputeSunFactor(TimeOfDay);
            float ambientLight = Mathf.Lerp(_nightAmbient, _dayAmbient, sunFactor);

            // Update materials
            _voxelMaterial.SetFloat(s_sunLightFactorId, sunFactor);
            _voxelMaterial.SetFloat(s_ambientLightId, ambientLight);

            if (_cutoutMaterial != null)
            {
                _cutoutMaterial.SetFloat(s_sunLightFactorId, sunFactor);
                _cutoutMaterial.SetFloat(s_ambientLightId, ambientLight);
            }

            if (_translucentMaterial != null)
            {
                _translucentMaterial.SetFloat(s_sunLightFactorId, sunFactor);
                _translucentMaterial.SetFloat(s_ambientLightId, ambientLight);
            }

            for (int i = 0; i < _additionalMaterials.Count; i++)
            {
                _additionalMaterials[i].SetFloat(s_sunLightFactorId, sunFactor);
                _additionalMaterials[i].SetFloat(s_ambientLightId, ambientLight);
            }

            // Update directional light rotation (intensity is fixed; voxel light system handles brightness)
            if (DirectionalLight != null)
            {
                float sunAngle = TimeOfDay * 360.0f + _sunAngleOffset;
                DirectionalLight.transform.rotation = Quaternion.Euler(sunAngle, _sunAzimuth, 0.0f);
                DirectionalLight.intensity = 1.0f;
            }
        }

        /// <summary>Sets the time of day directly, wrapping to [0,1) range.</summary>
        public void SetTimeOfDay(float time)
        {
            TimeOfDay = time % 1f;

            if (TimeOfDay < 0f)
            {
                TimeOfDay += 1f;
            }
        }

        /// <summary>Initializes all day/night settings, assigns materials, and finds the directional light.</summary>
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
            _dayAmbient = settings.DayAmbient;
            _nightAmbient = settings.NightAmbient;
            TimeOfDay = settings.StartTimeOfDay;

            // Find or create directional light
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    DirectionalLight = lights[i];

                    break;
                }
            }
        }

        /// <summary>
        ///     Registers an additional material to receive _SunLightFactor updates each frame.
        ///     Used by first-person arm and held item materials.
        /// </summary>
        public void RegisterMaterial(Material material)
        {
            if (material != null && !_additionalMaterials.Contains(material))
            {
                _additionalMaterials.Add(material);
            }
        }

        /// <summary>
        ///     Advances time-of-day by the given delta. Called at fixed tick rate
        ///     by TimeOfDayTickAdapter. Visual updates (materials, light) remain
        ///     in Update() at frame rate for smooth interpolation.
        /// </summary>
        public void AdvanceTick(float tickDt)
        {
            TimeOfDay += tickDt / _dayLengthSeconds;

            if (TimeOfDay >= 1.0f)
            {
                TimeOfDay -= 1.0f;
            }
        }

        /// <summary>Computes the sun brightness factor from the AnimationCurve or cosine fallback.</summary>
        private float ComputeSunFactor(float time)
        {
            // Use AnimationCurve if provided
            if (_dayNightCurve is
                {
                    length: > 0,
                })
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
