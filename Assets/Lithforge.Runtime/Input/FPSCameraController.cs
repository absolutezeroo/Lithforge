using UnityEngine;

namespace Lithforge.Runtime.Input
{
    public sealed class FPSCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float fastMoveSpeed = 50f;
        [SerializeField] private float lookSensitivity = 2f;

        private float _pitch;
        private float _yaw;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleMovement();
            HandleCursorToggle();
        }

        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            float mouseX = UnityEngine.Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * lookSensitivity;

            _yaw += mouseX;
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMovement()
        {
            float speed = UnityEngine.Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;

            Vector3 direction = Vector3.zero;

            if (UnityEngine.Input.GetKey(KeyCode.W))
            {
                direction += transform.forward;
            }

            if (UnityEngine.Input.GetKey(KeyCode.S))
            {
                direction -= transform.forward;
            }

            if (UnityEngine.Input.GetKey(KeyCode.A))
            {
                direction -= transform.right;
            }

            if (UnityEngine.Input.GetKey(KeyCode.D))
            {
                direction += transform.right;
            }

            if (UnityEngine.Input.GetKey(KeyCode.Space))
            {
                direction += Vector3.up;
            }

            if (UnityEngine.Input.GetKey(KeyCode.LeftControl))
            {
                direction -= Vector3.up;
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                direction.Normalize();
            }

            transform.position += direction * speed * Time.deltaTime;
        }

        private void HandleCursorToggle()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
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
