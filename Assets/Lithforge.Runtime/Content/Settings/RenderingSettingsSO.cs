using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "RenderingSettings", menuName = "Lithforge/Settings/Rendering", order = 2)]
    public sealed class RenderingSettingsSO : ScriptableObject
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
    }
}
