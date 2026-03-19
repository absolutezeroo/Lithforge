using System;

using Lithforge.Runtime.Simulation;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    ///     Aggregates all per-remote-player state: interpolation buffer, animator,
    ///     GPU transform buffer, skin texture, and name tag mesh. Each entity is
    ///     created on spawn and disposed on despawn or timeout.
    ///     Static model geometry (vertex/index buffers) is shared across all entities
    ///     via <see cref="RemotePlayerManager" />. Only the per-frame transform buffer
    ///     and skin texture are per-entity.
    /// </summary>
    public sealed class RemotePlayerEntity : IDisposable
    {
        /// <summary>Destroy entity if no snapshot arrives for this long.</summary>
        public const float TimeoutSeconds = 5f;

        /// <summary>Drives walk animation from interpolated position deltas.</summary>
        public readonly RemotePlayerAnimator Animator;

        /// <summary>Billboard quad mesh for rendering the player name above the entity.</summary>
        public readonly Mesh NameTagMesh;

        /// <summary>Per-entity GPU StructuredBuffer holding 6 world-space part transform matrices.</summary>
        public readonly GraphicsBuffer PartTransformsBuffer;

        /// <summary>CPU-side staging array for uploading part transforms to the GPU.</summary>
        public readonly Matrix4x4[] PartTransformUpload;

        /// <summary>Network player ID assigned by the server.</summary>
        public readonly ushort PlayerId;

        /// <summary>Display name of the remote player.</summary>
        public readonly string PlayerName;

        /// <summary>The 64x64 skin texture for this player entity.</summary>
        public readonly Texture2D SkinTexture;

        /// <summary>Ring buffer of timestamped snapshots for smooth interpolation.</summary>
        public readonly InterpolationBuffer<RemotePlayerSnapshot> SnapshotBuffer;

        /// <summary>True after Dispose has been called.</summary>
        private bool _disposed;

        /// <summary>Render parameters for the base (inner) model layer draw call.</summary>
        public RenderParams BaseModelParams;

        /// <summary>Server timestamp of the most recently received snapshot.</summary>
        public float LastSnapshotTime;

        /// <summary>Render parameters for the name tag billboard draw call.</summary>
        public RenderParams NameTagParams;

        /// <summary>Render parameters for the overlay (outer) model layer draw call.</summary>
        public RenderParams OverlayModelParams;

        /// <summary>Seconds since the last snapshot was received; entity is despawned when this exceeds TimeoutSeconds.</summary>
        public float TimeoutTimer;

        /// <summary>
        ///     Creates a remote player entity with initial snapshot, GPU transform buffer,
        ///     skin texture, name tag mesh, and render parameters for all draw layers.
        /// </summary>
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
                Position = initialPosition, Yaw = yaw, Pitch = pitch, Flags = flags,
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

        /// <summary>Releases the per-entity GPU transform buffer, skin texture, and name tag mesh.</summary>
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
                Object.Destroy(SkinTexture);
            }

            if (NameTagMesh != null)
            {
                Object.Destroy(NameTagMesh);
            }
        }

        /// <summary>
        ///     Pushes a new snapshot into the interpolation buffer and resets the timeout timer.
        /// </summary>
        public void PushSnapshot(float serverTimestamp, RemotePlayerSnapshot snapshot)
        {
            SnapshotBuffer.Push(serverTimestamp, snapshot);
            LastSnapshotTime = serverTimestamp;
            TimeoutTimer = 0f;
        }
    }
}
