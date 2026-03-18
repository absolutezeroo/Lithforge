using System;
using Lithforge.Runtime.Simulation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Aggregates all per-remote-player state: interpolation buffer, animator,
    /// GPU transform buffer, skin texture, and name tag mesh. Each entity is
    /// created on spawn and disposed on despawn or timeout.
    ///
    /// Static model geometry (vertex/index buffers) is shared across all entities
    /// via <see cref="RemotePlayerManager"/>. Only the per-frame transform buffer
    /// and skin texture are per-entity.
    /// </summary>
    public sealed class RemotePlayerEntity : IDisposable
    {
        /// <summary>Destroy entity if no snapshot arrives for this long.</summary>
        public const float TimeoutSeconds = 5f;

        public readonly ushort PlayerId;
        public readonly string PlayerName;

        // Interpolation
        public readonly InterpolationBuffer<RemotePlayerSnapshot> SnapshotBuffer;
        public float LastSnapshotTime;

        // Timeout safety net
        public float TimeoutTimer;

        // Animation
        public readonly RemotePlayerAnimator Animator;

        // GPU per-entity state
        public readonly GraphicsBuffer PartTransformsBuffer;
        public readonly Matrix4x4[] PartTransformUpload;

        // Render params (reference shared static buffers from RemotePlayerManager)
        public RenderParams BaseModelParams;
        public RenderParams OverlayModelParams;

        // Skin
        public readonly Texture2D SkinTexture;

        // Name tag
        public readonly Mesh NameTagMesh;
        public RenderParams NameTagParams;

        private bool _disposed;

        public RemotePlayerEntity(
            ushort playerId,
            string playerName,
            float3 initialPosition,
            float yaw,
            float pitch,
            byte flags,
            float initialTimestamp,
            Material baseModelMaterial,
            Material overlayModelMaterial,
            Material nameTagMaterial,
            Texture2D skinTexture)
        {
            PlayerId = playerId;
            PlayerName = playerName ?? "";
            SkinTexture = skinTexture;

            // Interpolation buffer
            SnapshotBuffer = new InterpolationBuffer<RemotePlayerSnapshot>();

            // Seed with initial snapshot at the calibrated render time so
            // interpolation starts immediately rather than waiting for clock sync
            RemotePlayerSnapshot initial = new()
            {
                Position = initialPosition,
                Yaw = yaw,
                Pitch = pitch,
                Flags = flags,
            };

            SnapshotBuffer.Push(initialTimestamp, initial);
            LastSnapshotTime = 0f;
            TimeoutTimer = 0f;

            // Animator
            Animator = new RemotePlayerAnimator(initialPosition);

            // Per-entity transform buffer (6 float4x4 matrices = 384 bytes)
            PartTransformsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured, 6, 64);
            PartTransformUpload = new Matrix4x4[6];

            for (int i = 0; i < 6; i++)
            {
                PartTransformUpload[i] = Matrix4x4.identity;
            }

            PartTransformsBuffer.SetData(PartTransformUpload);

            // RenderParams for base model layer
            BaseModelParams = new RenderParams(baseModelMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f)),
                matProps = new MaterialPropertyBlock(),
            };

            // RenderParams for overlay model layer
            OverlayModelParams = new RenderParams(overlayModelMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f)),
                matProps = new MaterialPropertyBlock(),
            };

            // Name tag mesh + params
            NameTagMesh = RemotePlayerNameTagBuilder.BuildMesh(playerName ?? "");

            NameTagParams = new RenderParams(nameTagMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f)),
                matProps = new MaterialPropertyBlock(),
            };
        }

        /// <summary>
        /// Pushes a new snapshot into the interpolation buffer and resets the timeout timer.
        /// </summary>
        public void PushSnapshot(float serverTimestamp, RemotePlayerSnapshot snapshot)
        {
            SnapshotBuffer.Push(serverTimestamp, snapshot);
            LastSnapshotTime = serverTimestamp;
            TimeoutTimer = 0f;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            PartTransformsBuffer?.Dispose();

            if (SkinTexture != null)
            {
                UnityEngine.Object.Destroy(SkinTexture);
            }

            if (NameTagMesh != null)
            {
                UnityEngine.Object.Destroy(NameTagMesh);
            }
        }
    }
}
