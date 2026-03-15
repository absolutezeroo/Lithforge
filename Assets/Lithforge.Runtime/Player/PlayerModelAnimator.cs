using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Computes 6 world-space float4x4 matrices per frame for the full player model.
    /// Parts: 0=head, 1=body, 2=rightArm, 3=leftArm, 4=rightLeg, 5=leftLeg.
    ///
    /// Body root is anchored at playerTransform.position in world space, rotated by camera yaw only.
    /// Each part transform: bodyRoot * T(pivot) * animRotation * T(-pivot).
    /// </summary>
    public sealed class PlayerModelAnimator
    {
        // Part pivots in block units (model pixels / 16), relative to body root (feet position)
        // These match the Minecraft model spec
        private static readonly float3 s_headPivot = new float3(0f, 24f, 0f) / 16f;
        private static readonly float3 s_bodyPivot = new float3(0f, 24f, 0f) / 16f;
        private static readonly float3 s_rightArmPivot = new float3(-6f, 22f, 0f) / 16f;
        private static readonly float3 s_leftArmPivot = new float3(6f, 22f, 0f) / 16f;
        private static readonly float3 s_rightLegPivot = new float3(-2f, 12f, 0f) / 16f;
        private static readonly float3 s_leftLegPivot = new float3(2f, 12f, 0f) / 16f;

        // Body offset: shift the model backward (in body-local Z) so the camera sits
        // at the front of the face (eye position) rather than at the center of the head.
        // Without this, looking down shows only the body top face ("neck hole").
        // With it, looking down reveals the body front face (shirt), arms, and legs.
        private const float BodyBackwardOffset = 0.15f;

        // Walk animation parameters
        private const float WalkSwingArmDeg = 40f;
        private const float WalkSwingLegDeg = 45f;
        private const float WalkSpeedScale = 0.6f;

        // Swing animation parameters (mining/attack)
        private const float SwingDuration = 0.3f;
        private const float SwingPitchDeg = -80f;
        private const float SwingYawDeg = -20f;
        private const float SwingRollDeg = -20f;

        // Equip animation parameters
        private const float EquipDuration = 0.2f;
        private const float EquipDropDeg = 40f;

        private readonly Transform _playerTransform;
        private readonly Transform _cameraTransform;

        // Walk state
        private float3 _lastPlayerPos;
        private float _walkPhase;

        // Swing state
        private bool _isSwinging;
        private float _swingTimer;
        private bool _wasMining;

        // Equip state
        private bool _isEquipping;
        private float _equipTimer;

        // Output matrices (indices: 0=head, 1=body, 2=rightArm, 3=leftArm, 4=rightLeg, 5=leftLeg)
        private readonly float4x4[] _partTransforms = new float4x4[6];

        public PlayerModelAnimator(Transform playerTransform, Transform cameraTransform)
        {
            _playerTransform = playerTransform;
            _cameraTransform = cameraTransform;
            _lastPlayerPos = ((float3)playerTransform.position);

            for (int i = 0; i < 6; i++)
            {
                _partTransforms[i] = float4x4.identity;
            }
        }

        /// <summary>
        /// The 6 part transform matrices (world-space). Updated each frame by <see cref="Update"/>.
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
        /// Updates all animation state and outputs world-space part transform matrices.
        /// </summary>
        public void Update(float deltaTime, bool isMining, bool isOnGround, bool isFlying)
        {
            // Detect mining start for swing trigger
            if (isMining && !_wasMining)
            {
                TriggerSwing();
            }
            _wasMining = isMining;

            // Re-trigger swing while mining
            if (isMining && !_isSwinging)
            {
                TriggerSwing();
            }

            UpdateWalkPhase(deltaTime, isOnGround, isFlying);
            UpdateSwing(deltaTime);
            UpdateEquip(deltaTime);

            // Compute body root: TRS(playerPosition, yawOnly, scale=1)
            float cameraYaw = _cameraTransform.eulerAngles.y;
            float cameraPitch = _cameraTransform.localEulerAngles.x;

            // Normalize pitch to [-180, 180]
            if (cameraPitch > 180f)
            {
                cameraPitch -= 360f;
            }

            float3 playerPos = ((float3)_playerTransform.position);

            // Body root: position at player feet, rotate by yaw, then shift backward
            // so the camera (at eye height) sits at the front of the face.
            // T(playerPos) * RotY(yaw) * T(0, 0, -offset)
            float4x4 bodyRoot = math.mul(
                float4x4.Translate(playerPos),
                math.mul(
                    float4x4.RotateY(math.radians(cameraYaw)),
                    float4x4.Translate(new float3(0f, 0f, -BodyBackwardOffset))));

            // Walk swing angles
            float walkSin = math.sin(_walkPhase * math.PI);
            float armSwingRad = math.radians(WalkSwingArmDeg * walkSin);
            float legSwingRad = math.radians(WalkSwingLegDeg * walkSin);

            // Head: pitch follows camera
            _partTransforms[0] = ComputePartMatrix(
                bodyRoot, s_headPivot,
                float4x4.RotateX(math.radians(cameraPitch)));

            // Body: identity rotation (yaw is in bodyRoot)
            _partTransforms[1] = ComputePartMatrix(
                bodyRoot, s_bodyPivot,
                float4x4.identity);

            // Right Arm: walk swing (opposite to left leg) + mining swing + equip
            float4x4 rightArmAnim = ComputeRightArmAnimation(-armSwingRad);
            _partTransforms[2] = ComputePartMatrix(
                bodyRoot, s_rightArmPivot,
                rightArmAnim);

            // Left Arm: walk swing (opposite to right leg)
            _partTransforms[3] = ComputePartMatrix(
                bodyRoot, s_leftArmPivot,
                float4x4.RotateX(armSwingRad));

            // Right Leg: walk swing
            _partTransforms[4] = ComputePartMatrix(
                bodyRoot, s_rightLegPivot,
                float4x4.RotateX(legSwingRad));

            // Left Leg: walk swing (opposite)
            _partTransforms[5] = ComputePartMatrix(
                bodyRoot, s_leftLegPivot,
                float4x4.RotateX(-legSwingRad));
        }

        private void UpdateWalkPhase(float deltaTime, bool isOnGround, bool isFlying)
        {
            float3 currentPos = ((float3)_playerTransform.position);
            float3 delta = currentPos - _lastPlayerPos;
            _lastPlayerPos = currentPos;

            float horizontalDist = math.sqrt(delta.x * delta.x + delta.z * delta.z);

            if (isOnGround && !isFlying && horizontalDist > 0.001f)
            {
                _walkPhase += horizontalDist * WalkSpeedScale;
            }
            else
            {
                // Decay walk phase toward nearest whole number so limbs ease to rest
                float target = math.round(_walkPhase);
                _walkPhase = math.lerp(_walkPhase, target, math.saturate(deltaTime * 5f));
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

        /// <summary>
        /// Computes the final world-space matrix for a body part.
        /// Formula: bodyRoot * T(pivot) * animRotation
        /// Vertices are stored pivot-relative in the mesh (position - pivot) / 16,
        /// so T(-pivot) is NOT needed — the pivot offset is already baked out.
        /// </summary>
        private static float4x4 ComputePartMatrix(float4x4 bodyRoot, float3 pivot, float4x4 animRotation)
        {
            float4x4 toPivot = float4x4.Translate(pivot);

            return math.mul(bodyRoot, math.mul(toPivot, animRotation));
        }

        /// <summary>
        /// Computes the right arm animation rotation combining walk swing, mining swing, and equip.
        /// </summary>
        private float4x4 ComputeRightArmAnimation(float walkSwingRad)
        {
            float4x4 result = float4x4.RotateX(walkSwingRad);

            // Equip animation: pitch drop and recovery
            if (_isEquipping)
            {
                float progress = math.saturate(_equipTimer / EquipDuration);
                float drop = (1f - progress);
                drop = drop * drop * drop; // cubic ease
                result = math.mul(result, float4x4.RotateX(math.radians(EquipDropDeg * drop)));
            }

            // Swing animation (mining/attack)
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

                result = math.mul(result, swingRot);
            }

            return result;
        }
    }
}
