using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Mouse look controller. Attached to the Camera child of the player.
    /// Handles pitch (local X rotation) while the parent PlayerController handles yaw.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [FormerlySerializedAs("lookSensitivity")]
        [SerializeField] private float _lookSensitivity = 0.1f;

        private float _pitch;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _pitch = transform.localEulerAngles.x;

            if (_pitch > 180f)
            {
                _pitch -= 360f;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();

            // Pitch: rotate camera locally on X axis (clamped)
            _pitch -= delta.y * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Yaw: rotate parent on Y axis (so movement follows look direction)
            Transform parent = transform.parent;

            if (parent != null)
            {
                float yaw = parent.eulerAngles.y + delta.x * _lookSensitivity;
                parent.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        public float LookSensitivity
        {
            get { return _lookSensitivity; }
        }

        public void SetLookSensitivity(float value)
        {
            _lookSensitivity = Mathf.Clamp(value, 0.01f, 2.0f);
        }

        /// <summary>
        /// Gets the camera's forward direction in world space.
        /// </summary>
        public Vector3 GetLookDirection()
        {
            return transform.forward;
        }

        /// <summary>
        /// Gets the camera's world position.
        /// </summary>
        public Vector3 GetEyePosition()
        {
            return transform.position;
        }
    }
}
