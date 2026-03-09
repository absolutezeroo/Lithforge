using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    public sealed class TimeOfDayController : MonoBehaviour
    {
        [SerializeField] private float dayLengthSeconds = 600f;

        private float _timeOfDay;
        private Light _directionalLight;
        private Material _voxelMaterial;

        private static readonly int _sunLightFactorId = Shader.PropertyToID("_SunLightFactor");

        public float TimeOfDay
        {
            get { return _timeOfDay; }
        }

        public float SunLightFactor
        {
            get { return ComputeSunFactor(_timeOfDay); }
        }

        public void Initialize(Material voxelMaterial)
        {
            _voxelMaterial = voxelMaterial;
            _timeOfDay = 0.25f; // Start at noon (0=midnight, 0.25=sunrise, 0.5=noon, 0.75=sunset)

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

        private void Update()
        {
            if (_voxelMaterial == null)
            {
                return;
            }

            // Advance time
            _timeOfDay += Time.deltaTime / dayLengthSeconds;

            if (_timeOfDay >= 1.0f)
            {
                _timeOfDay -= 1.0f;
            }

            float sunFactor = ComputeSunFactor(_timeOfDay);

            // Update material
            _voxelMaterial.SetFloat(_sunLightFactorId, sunFactor);

            // Update directional light rotation
            if (_directionalLight != null)
            {
                float sunAngle = _timeOfDay * 360.0f - 90.0f;
                _directionalLight.transform.rotation = Quaternion.Euler(sunAngle, -30.0f, 0.0f);
                _directionalLight.intensity = Mathf.Max(0.1f, sunFactor);
            }
        }

        private static float ComputeSunFactor(float time)
        {
            // Cosine curve: 1.0 at noon (time=0.5), 0.15 at midnight (time=0.0 or 1.0)
            float cosValue = Mathf.Cos(time * 2.0f * Mathf.PI);
            float factor = 0.575f + 0.425f * cosValue;

            return Mathf.Clamp(factor, 0.15f, 1.0f);
        }
    }
}
