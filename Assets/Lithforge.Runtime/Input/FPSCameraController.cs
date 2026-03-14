using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Input
{
    public sealed class FPSCameraController : MonoBehaviour
    {
        [FormerlySerializedAs("_moveSpeed"),SerializeField] private float moveSpeed = 10f;
        [FormerlySerializedAs("_fastMoveSpeed"),SerializeField] private float fastMoveSpeed = 50f;
        [FormerlySerializedAs("_lookSensitivity"),SerializeField] private float lookSensitivity = 0.1f;

        private float _pitch;
        private float _yaw;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            if (_pitch > 180f)
            {
                _pitch -= 360f;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null || mouse == null)
            {
                return;
            }

            HandleCursorToggle(keyboard);
            HandleMouseLook(mouse);
            HandleMovement(keyboard);
        }

        private void HandleMouseLook(Mouse mouse)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();

            _yaw += delta.x * lookSensitivity;
            _pitch -= delta.y * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMovement(Keyboard keyboard)
        {
            float speed = keyboard.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;

            Vector3 direction = Vector3.zero;

            if (keyboard.wKey.isPressed)
            {
                direction += transform.forward;
            }

            if (keyboard.sKey.isPressed)
            {
                direction -= transform.forward;
            }

            if (keyboard.aKey.isPressed)
            {
                direction -= transform.right;
            }

            if (keyboard.dKey.isPressed)
            {
                direction += transform.right;
            }

            if (keyboard.spaceKey.isPressed)
            {
                direction += Vector3.up;
            }

            if (keyboard.leftCtrlKey.isPressed)
            {
                direction -= Vector3.up;
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                direction.Normalize();
            }

            transform.position += direction * speed * Time.deltaTime;
        }

        private void HandleCursorToggle(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
}
