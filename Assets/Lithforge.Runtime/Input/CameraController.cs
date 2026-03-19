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
        /// <summary>Mouse look sensitivity multiplier, configurable from settings.</summary>
        [FormerlySerializedAs("_lookSensitivity"),SerializeField] private float lookSensitivity = 0.1f;

        /// <summary>Current pitch angle in degrees, clamped to [-89, 89].</summary>
        private float _pitch;

        /// <summary>Locks the cursor and initializes the pitch from the current rotation.</summary>
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

        /// <summary>Reads mouse delta each frame to update pitch and yaw rotations.</summary>
        private void Update()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();

            // Pitch: rotate camera locally on X axis (clamped)
            _pitch -= delta.y * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Yaw: rotate parent on Y axis (so movement follows look direction)
            Transform parent = transform.parent;

            if (parent != null)
            {
                float yaw = parent.eulerAngles.y + delta.x * lookSensitivity;
                parent.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        /// <summary>Gets the current mouse look sensitivity multiplier.</summary>
        public float LookSensitivity
        {
            get { return lookSensitivity; }
        }

        public void SetLookSensitivity(float value)
        {
            lookSensitivity = Mathf.Clamp(value, 0.01f, 2.0f);
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
