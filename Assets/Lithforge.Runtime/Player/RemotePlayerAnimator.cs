using Unity.Mathematics;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Computes 6 world-space float4x4 matrices per frame for a remote player model.
    /// Mirrors <see cref="PlayerModelAnimator"/> logic but accepts plain values
    /// (float3 position, float yaw, float pitch) instead of Transform references,
    /// making it safe for remote entities driven by interpolated snapshots.
    ///
    /// Parts: 0=head, 1=body, 2=rightArm, 3=leftArm (main hand), 4=rightLeg, 5=leftLeg.
    /// Walk animation is driven by position delta. No swing or equip animation
    /// (remote players do not show held items in this version).
    /// </summary>
    public sealed class RemotePlayerAnimator
    {
        // Part pivots in block units (model pixels / 16), matching PlayerModelAnimator
        private static readonly float3 s_headPivot = new float3(0f, 24f, 0f) / 16f;
        private static readonly float3 s_bodyPivot = new float3(0f, 24f, 0f) / 16f;
        private static readonly float3 s_rightArmPivot = new float3(-6f, 22f, 0f) / 16f;
        private static readonly float3 s_leftArmPivot = new float3(6f, 22f, 0f) / 16f;
        private static readonly float3 s_rightLegPivot = new float3(-2f, 12f, 0f) / 16f;
        private static readonly float3 s_leftLegPivot = new float3(2f, 12f, 0f) / 16f;

        private const float WalkSwingArmDeg = 40f;
        private const float WalkSwingLegDeg = 45f;
        private const float WalkSpeedScale = 0.6f;

        private float3 _lastPosition;
        private float _walkPhase;

        private readonly float4x4[] _partTransforms = new float4x4[6];

        /// <summary>
        /// The 6 part transform matrices (world-space). Updated each frame by <see cref="Update"/>.
        /// </summary>
        public float4x4[] PartTransforms
        {
            get { return _partTransforms; }
        }

        public RemotePlayerAnimator(float3 initialPosition)
        {
            _lastPosition = initialPosition;

            for (int i = 0; i < 6; i++)
            {
                _partTransforms[i] = float4x4.identity;
            }
        }

        /// <summary>
        /// Updates all animation state and outputs world-space part transform matrices.
        /// All inputs are plain values — no Transform or camera references.
        /// </summary>
        public void Update(
            float deltaTime,
            float3 position,
            float yaw,
            float pitch,
            bool isOnGround,
            bool isFlying)
        {
            UpdateWalkPhase(deltaTime, position, isOnGround, isFlying);

            // Body root: T(position) * RotY(yaw)
            // No backward offset for remote players (they're viewed from outside)
            float4x4 bodyRoot = math.mul(
                float4x4.Translate(position),
                float4x4.RotateY(math.radians(yaw)));

            // Walk swing angles
            float walkSin = math.sin(_walkPhase * math.PI);
            float armSwingRad = math.radians(WalkSwingArmDeg * walkSin);
            float legSwingRad = math.radians(WalkSwingLegDeg * walkSin);

            // Head: pitch follows interpolated value
            _partTransforms[0] = ComputePartMatrix(
                bodyRoot, s_headPivot,
                float4x4.RotateX(math.radians(pitch)));

            // Body: identity rotation (yaw is in bodyRoot)
            _partTransforms[1] = ComputePartMatrix(
                bodyRoot, s_bodyPivot,
                float4x4.identity);

            // Right Arm (off-hand): walk swing only
            _partTransforms[2] = ComputePartMatrix(
                bodyRoot, s_rightArmPivot,
                float4x4.RotateX(armSwingRad));

            // Left Arm (main hand): opposite walk swing
            _partTransforms[3] = ComputePartMatrix(
                bodyRoot, s_leftArmPivot,
                float4x4.RotateX(-armSwingRad));

            // Right Leg: walk swing
            _partTransforms[4] = ComputePartMatrix(
                bodyRoot, s_rightLegPivot,
                float4x4.RotateX(legSwingRad));

            // Left Leg: walk swing (opposite)
            _partTransforms[5] = ComputePartMatrix(
                bodyRoot, s_leftLegPivot,
                float4x4.RotateX(-legSwingRad));
        }

        private void UpdateWalkPhase(float deltaTime, float3 currentPos, bool isOnGround, bool isFlying)
        {
            float3 delta = currentPos - _lastPosition;
            _lastPosition = currentPos;

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

        /// <summary>
        /// Computes the final world-space matrix for a body part.
        /// Formula: bodyRoot * T(pivot) * animRotation.
        /// Vertices are stored pivot-relative in the mesh, so T(-pivot) is NOT needed.
        /// </summary>
        private static float4x4 ComputePartMatrix(float4x4 bodyRoot, float3 pivot, float4x4 animRotation)
        {
            float4x4 toPivot = float4x4.Translate(pivot);
            return math.mul(bodyRoot, math.mul(toPivot, animRotation));
        }
    }
}
