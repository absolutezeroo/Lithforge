using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Computes 6 world-space float4x4 matrices per frame for the full player model.
    /// Parts: 0=head, 1=body, 2=rightArm (off-hand, -X), 3=leftArm (main hand, +X), 4=rightLeg, 5=leftLeg.
    ///
    /// Body root is anchored at playerTransform.position in world space, rotated by camera yaw only.
    /// Each part transform: bodyRoot * T(pivot) * animRotation * T(-pivot).
    /// </summary>
    public sealed class PlayerModelAnimator
    {
        /// <summary>Head pivot in block units (model pixels / 16), relative to body root.</summary>
        private static readonly float3 s_headPivot = new float3(0f, 24f, 0f) / 16f;

        /// <summary>Body pivot in block units (model pixels / 16), relative to body root.</summary>
        private static readonly float3 s_bodyPivot = new float3(0f, 24f, 0f) / 16f;

        /// <summary>Right arm pivot in block units (model pixels / 16), relative to body root.</summary>
        private static readonly float3 s_rightArmPivot = new float3(-6f, 22f, 0f) / 16f;

        /// <summary>Left arm (main hand) pivot in block units (model pixels / 16), relative to body root.</summary>
        private static readonly float3 s_leftArmPivot = new float3(6f, 22f, 0f) / 16f;

        /// <summary>Right leg pivot in block units (model pixels / 16), relative to body root.</summary>
        private static readonly float3 s_rightLegPivot = new float3(-2f, 12f, 0f) / 16f;

        /// <summary>Left leg pivot in block units (model pixels / 16), relative to body root.</summary>
        private static readonly float3 s_leftLegPivot = new float3(2f, 12f, 0f) / 16f;

        /// <summary>
        ///     Body-local Z offset so the camera sits at the front of the face (eye position)
        ///     rather than at the center of the head. Reveals the shirt, arms, and legs when looking down.
        /// </summary>
        private const float BodyBackwardOffset = 0.15f;

        /// <summary>Maximum arm swing angle in degrees during walking.</summary>
        private const float WalkSwingArmDeg = 40f;

        /// <summary>Maximum leg swing angle in degrees during walking.</summary>
        private const float WalkSwingLegDeg = 45f;

        /// <summary>Multiplier converting horizontal distance to walk phase advancement.</summary>
        private const float WalkSpeedScale = 0.6f;

        /// <summary>Duration of the mining/attack swing animation in seconds.</summary>
        private const float SwingDuration = 0.3f;

        /// <summary>Peak pitch rotation in degrees during the swing animation.</summary>
        private const float SwingPitchDeg = -80f;

        /// <summary>Peak yaw rotation in degrees during the swing animation.</summary>
        private const float SwingYawDeg = -20f;

        /// <summary>Peak roll rotation in degrees during the swing animation.</summary>
        private const float SwingRollDeg = -20f;

        /// <summary>Duration of the equip (item change) animation in seconds.</summary>
        private const float EquipDuration = 0.2f;

        /// <summary>Arm drop angle in degrees at the start of the equip animation.</summary>
        private const float EquipDropDeg = 40f;

        /// <summary>Player body transform, used for world position and yaw.</summary>
        private readonly Transform _playerTransform;

        /// <summary>Camera transform, used for yaw and pitch extraction.</summary>
        private readonly Transform _cameraTransform;

        /// <summary>World position from the previous frame, used to compute horizontal movement delta.</summary>
        private float3 _lastPlayerPos;

        /// <summary>Continuously advancing walk cycle phase; sin(_walkPhase * PI) drives limb swing.</summary>
        private float _walkPhase;

        /// <summary>True while the swing (mining/attack) animation is playing.</summary>
        private bool _isSwinging;

        /// <summary>Elapsed time in the current swing animation.</summary>
        private float _swingTimer;

        /// <summary>Mining state from the previous frame, used to detect mining start edges.</summary>
        private bool _wasMining;

        /// <summary>True while the equip (item change) animation is playing.</summary>
        private bool _isEquipping;

        /// <summary>Elapsed time in the current equip animation.</summary>
        private float _equipTimer;

        /// <summary>Creates the animator with references to the player body and camera transforms.</summary>
        public PlayerModelAnimator(Transform playerTransform, Transform cameraTransform)
        {
            _playerTransform = playerTransform;
            _cameraTransform = cameraTransform;
            _lastPlayerPos = ((float3)playerTransform.position);

            for (int i = 0; i < 6; i++)
            {
                PartTransforms[i] = float4x4.identity;
            }
        }

        /// <summary>
        /// The 6 part transform matrices (world-space). Updated each frame by <see cref="Update"/>.
        /// </summary>
        public float4x4[] PartTransforms { get; } = new float4x4[6];

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
            PartTransforms[0] = ComputePartMatrix(
                bodyRoot, s_headPivot,
                float4x4.RotateX(math.radians(cameraPitch)));

            // Body: identity rotation (yaw is in bodyRoot)
            PartTransforms[1] = ComputePartMatrix(
                bodyRoot, s_bodyPivot,
                float4x4.identity);

            // Right Arm (off-hand, -X = left side of screen): walk swing only
            PartTransforms[2] = ComputePartMatrix(
                bodyRoot, s_rightArmPivot,
                float4x4.RotateX(armSwingRad));

            // Left Arm (main hand, +X = right side of screen): walk swing + mining swing + equip
            float4x4 mainArmAnim = ComputeMainArmAnimation(-armSwingRad);
            PartTransforms[3] = ComputePartMatrix(
                bodyRoot, s_leftArmPivot,
                mainArmAnim);

            // Right Leg: walk swing
            PartTransforms[4] = ComputePartMatrix(
                bodyRoot, s_rightLegPivot,
                float4x4.RotateX(legSwingRad));

            // Left Leg: walk swing (opposite)
            PartTransforms[5] = ComputePartMatrix(
                bodyRoot, s_leftLegPivot,
                float4x4.RotateX(-legSwingRad));
        }

        /// <summary>Advances or decays the walk phase based on horizontal movement distance.</summary>
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

        /// <summary>Advances the swing timer and clears the animation when it expires.</summary>
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

        /// <summary>Advances the equip timer and clears the animation when it expires.</summary>
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
        /// Computes the main arm (partID=3, +X side) animation rotation combining walk swing, mining swing, and equip.
        /// </summary>
        private float4x4 ComputeMainArmAnimation(float walkSwingRad)
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
