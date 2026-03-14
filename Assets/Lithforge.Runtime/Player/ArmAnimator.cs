using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Computes per-part transform matrices for first-person arm rendering.
    /// Handles view bobbing (walk-driven), swing animation (mining/attack),
    /// and equip animation (held item change).
    /// Outputs 6 float4x4 matrices each frame (only indices 2 and 3 used for arms).
    /// </summary>
    public sealed class ArmAnimator
    {
        // Base arm positions in camera-local space (model units → block units / 16)
        private static readonly float3 RightArmOffset = new float3(0.56f, -0.52f, -0.72f);
        private static readonly float3 LeftArmOffset = new float3(-0.56f, -0.52f, -0.72f);

        // View bob parameters
        private const float BobStrength = 0.015f;
        private const float BobSpeedScale = 0.6f;

        // Swing animation parameters
        private const float SwingDuration = 0.3f;
        private const float SwingPitchDeg = -80f;
        private const float SwingYawDeg = -20f;
        private const float SwingRollDeg = -20f;

        // Equip animation parameters
        private const float EquipDuration = 0.2f;
        private const float EquipDropAmount = 0.6f;

        private readonly Transform _playerTransform;

        // Walk bob state (computed from player position delta)
        private float3 _lastPlayerPos;
        private float _walkDistance;

        // Swing state
        private bool _isSwinging;
        private float _swingTimer;
        private bool _wasMinig;

        // Equip state
        private bool _isEquipping;
        private float _equipTimer;

        // Output matrices (indices: 0=head, 1=body, 2=rightArm, 3=leftArm, 4=rightLeg, 5=leftLeg)
        private readonly float4x4[] _partTransforms = new float4x4[6];

        public ArmAnimator(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            _lastPlayerPos = ((float3)playerTransform.position);

            for (int i = 0; i < 6; i++)
            {
                _partTransforms[i] = float4x4.identity;
            }
        }

        /// <summary>
        /// The 6 part transform matrices. Updated each frame by <see cref="Update"/>.
        /// </summary>
        public float4x4[] PartTransforms
        {
            get { return _partTransforms; }
        }

        /// <summary>
        /// Triggers the swing animation (called when mining starts or on left-click attack).
        /// </summary>
        public void TriggerSwing()
        {
            _isSwinging = true;
            _swingTimer = 0f;
        }

        /// <summary>
        /// Triggers the equip animation (called when held item changes).
        /// </summary>
        public void TriggerEquip()
        {
            _isEquipping = true;
            _equipTimer = 0f;
        }

        /// <summary>
        /// Updates all animation state and outputs part transform matrices.
        /// Call each frame from the arm renderer.
        /// </summary>
        public void Update(float deltaTime, bool isMining, bool isOnGround, bool isFlying)
        {
            // Detect mining start for swing trigger
            if (isMining && !_wasMinig)
            {
                TriggerSwing();
            }
            _wasMinig = isMining;

            // Re-trigger swing while mining
            if (isMining && !_isSwinging)
            {
                TriggerSwing();
            }

            UpdateWalkBob(deltaTime, isOnGround, isFlying);
            UpdateSwing(deltaTime);
            UpdateEquip(deltaTime);

            // Compute final matrices for each arm
            _partTransforms[2] = ComputeArmMatrix(RightArmOffset);
            _partTransforms[3] = ComputeArmMatrix(LeftArmOffset);
        }

        private void UpdateWalkBob(float deltaTime, bool isOnGround, bool isFlying)
        {
            float3 currentPos = ((float3)_playerTransform.position);
            float3 delta = currentPos - _lastPlayerPos;
            _lastPlayerPos = currentPos;

            // Only bob when on ground and moving horizontally
            float horizontalDist = math.sqrt(delta.x * delta.x + delta.z * delta.z);

            if (isOnGround && !isFlying && horizontalDist > 0.001f)
            {
                _walkDistance += horizontalDist * BobSpeedScale;
            }
            else
            {
                // Slowly decay walk distance so bob eases out
                _walkDistance += deltaTime * 0.5f;
            }
        }

        private void UpdateSwing(float deltaTime)
        {
            if (!_isSwinging)
            {
                return;
            }

            _swingTimer += deltaTime;

            if (_swingTimer >= SwingDuration)
            {
                _isSwinging = false;
                _swingTimer = 0f;
            }
        }

        private void UpdateEquip(float deltaTime)
        {
            if (!_isEquipping)
            {
                return;
            }

            _equipTimer += deltaTime;

            if (_equipTimer >= EquipDuration)
            {
                _isEquipping = false;
                _equipTimer = 0f;
            }
        }

        private float4x4 ComputeArmMatrix(float3 baseOffset)
        {
            // Start with identity
            float4x4 mat = float4x4.identity;

            // 1. View bobbing translation
            float bobX = math.sin(_walkDistance * math.PI) * BobStrength * 0.5f;
            float bobY = -math.abs(math.cos(_walkDistance * math.PI) * BobStrength);
            mat = math.mul(float4x4.Translate(new float3(bobX, bobY, 0f)), mat);

            // 2. Base arm position
            mat = math.mul(float4x4.Translate(baseOffset), mat);

            // 3. Equip animation (drop and rise)
            if (_isEquipping)
            {
                float progress = math.saturate(_equipTimer / EquipDuration);
                float drop = (1f - progress);
                drop = drop * drop * drop; // cubic ease
                mat = math.mul(float4x4.Translate(new float3(0f, -drop * EquipDropAmount, 0f)), mat);
            }

            // 4. Swing animation (pitch/yaw/roll rotation)
            if (_isSwinging)
            {
                float t = math.saturate(_swingTimer / SwingDuration);
                float f = math.sin(math.sqrt(t) * math.PI);
                float f1 = math.sin(t * math.PI);

                float pitchRad = math.radians(SwingPitchDeg * f1);
                float yawRad = math.radians(SwingYawDeg * f);
                float rollRad = math.radians(SwingRollDeg * f);

                float4x4 swingRot = math.mul(
                    math.mul(
                        float4x4.RotateY(yawRad),
                        float4x4.RotateX(pitchRad)),
                    float4x4.RotateZ(rollRad));

                mat = math.mul(mat, swingRot);
            }

            return mat;
        }
    }
}
