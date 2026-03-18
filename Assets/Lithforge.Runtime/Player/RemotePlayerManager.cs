using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Lithforge.Runtime.Simulation;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    ///     Manages all remote player entities: spawn/despawn lifecycle, snapshot routing,
    ///     timeout sweeps, interpolation sampling, animation, and GPU draw calls.
    ///     Static model geometry (vertex/index/args buffers) is shared across all entities.
    ///     Per-entity cost is a 384-byte transform buffer, a skin texture, and a name tag mesh.
    ///     Owner: GameLoop (via LithforgeBootstrap). Lifetime: game session.
    /// </summary>
    public sealed class RemotePlayerManager : IDisposable
    {
        // Shader property IDs (same as PlayerRenderer)
        private static readonly int s_playerVertexBufferId = Shader.PropertyToID("_PlayerVertexBuffer");
        private static readonly int s_partTransformsId = Shader.PropertyToID("_PartTransforms");
        private static readonly int s_skinTexId = Shader.PropertyToID("_SkinTex");

        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds s_worldBounds = new(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        // Shared materials
        private readonly Material _baseModelMaterial;

        // Entities
        private readonly Dictionary<ushort, RemotePlayerEntity> _entities = new();
        private readonly Material _nameTagMaterial;
        private readonly Material _overlayModelMaterial;
        private readonly GraphicsBuffer _sharedArgsBuffer;
        private readonly GraphicsBuffer _sharedIndexBuffer;

        // Shared static GPU buffers (built once from PlayerModelMeshBuilder)
        private readonly GraphicsBuffer _sharedVertexBuffer;

        // Skin loader
        private readonly SkinLoader _skinLoader;

        // Timeout sweep cache (fill pattern)
        private readonly List<ushort> _timedOutIds = new();

        private bool _disposed;

        // Render time: offset so _renderTime aligns with server-tick-based timestamps
        private float _renderTime;
        private bool _renderTimeInitialized;
        private float _renderTimeOffset;

        public RemotePlayerManager(
            Material baseModelMaterial,
            Material overlayModelMaterial,
            Material nameTagMaterial)
        {
            _baseModelMaterial = baseModelMaterial;
            _overlayModelMaterial = overlayModelMaterial;
            _nameTagMaterial = nameTagMaterial;
            _skinLoader = new SkinLoader();

            // Build shared static geometry (third-person, non-slim)
            PlayerModelMeshBuilder.Build(false, out PlayerModelVertex[] modelVertices, out int[] modelIndices);

            _sharedVertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                PlayerModelMeshBuilder.TotalVertexCount,
                Marshal.SizeOf<PlayerModelVertex>());
            _sharedVertexBuffer.SetData(modelVertices);

            _sharedIndexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                PlayerModelMeshBuilder.TotalIndexCount,
                sizeof(int));
            _sharedIndexBuffer.SetData(modelIndices);

            // Indirect args: 2 draw commands (base layer + overlay layer)
            _sharedArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                2,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            GraphicsBuffer.IndirectDrawIndexedArgs[] args =
                new GraphicsBuffer.IndirectDrawIndexedArgs[2];

            // Base layer: all 6 parts (third-person, 216 indices)
            args[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = PlayerModelMeshBuilder.ThirdPersonLayerIndexCount,
                instanceCount = 1,
                startIndex = 0,
                baseVertexIndex = 0,
                startInstance = 0,
            };

            // Overlay layer: all 6 parts (third-person, 216 indices)
            args[1] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = PlayerModelMeshBuilder.ThirdPersonLayerIndexCount,
                instanceCount = 1,
                startIndex = PlayerModelMeshBuilder.ThirdPersonLayerIndexCount,
                baseVertexIndex = 0,
                startInstance = 0,
            };

            _sharedArgsBuffer.SetData(args);
        }

        /// <summary>
        ///     Number of currently tracked remote player entities.
        /// </summary>
        public int EntityCount
        {
            get { return _entities.Count; }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (KeyValuePair<ushort, RemotePlayerEntity> kvp in _entities)
            {
                kvp.Value.Dispose();
            }

            _entities.Clear();

            _sharedVertexBuffer?.Dispose();
            _sharedIndexBuffer?.Dispose();
            _sharedArgsBuffer?.Dispose();
        }

        /// <summary>
        ///     Spawns a new remote player entity. If an entity with the same ID already exists,
        ///     the old one is disposed and replaced. Seeds the initial snapshot with the current
        ///     calibrated render time so interpolation works from the first frame.
        /// </summary>
        public void SpawnPlayer(
            ushort playerId,
            string playerName,
            float3 position,
            float yaw,
            float pitch,
            byte flags)
        {
            if (_disposed)
            {
                return;
            }

            // Dispose existing entity if present (edge case: duplicate spawn)
            if (_entities.TryGetValue(playerId, out RemotePlayerEntity existing))
            {
                existing.Dispose();
                _entities.Remove(playerId);
            }

            // Load skin (placeholder: default skin for all players)
            Texture2D skinTexture = _skinLoader.LoadSkin("default.png");

            if (skinTexture == null)
            {
                skinTexture = _skinLoader.CreateDefaultSkin();
            }

            // Seed initial snapshot with calibrated render time so interpolation
            // buffer is in the same time space as incoming server timestamps
            float initialTimestamp = _renderTime + _renderTimeOffset;

            RemotePlayerEntity entity = new(
                playerId,
                playerName,
                position,
                yaw,
                pitch,
                flags,
                initialTimestamp,
                _baseModelMaterial,
                _overlayModelMaterial,
                _nameTagMaterial,
                skinTexture);

            _entities[playerId] = entity;
        }

        /// <summary>
        ///     Despawns and disposes the remote player entity with the given ID.
        ///     No-op if the entity does not exist.
        /// </summary>
        public void DespawnPlayer(ushort playerId)
        {
            if (_disposed)
            {
                return;
            }

            if (_entities.TryGetValue(playerId, out RemotePlayerEntity entity))
            {
                entity.Dispose();
                _entities.Remove(playerId);
            }
        }

        /// <summary>
        ///     Pushes a new snapshot into a remote player's interpolation buffer.
        ///     On the first snapshot received, calibrates the render clock offset so
        ///     that <c>_renderTime</c> aligns with server-tick-based timestamps.
        ///     No-op if the entity does not exist.
        /// </summary>
        public void PushSnapshot(ushort playerId, float serverTimestamp, RemotePlayerSnapshot snapshot)
        {
            if (_disposed)
            {
                return;
            }

            // Calibrate render clock on first snapshot so timestamps align
            if (!_renderTimeInitialized)
            {
                _renderTimeOffset = serverTimestamp - _renderTime;
                _renderTimeInitialized = true;
            }

            if (_entities.TryGetValue(playerId, out RemotePlayerEntity entity))
            {
                entity.PushSnapshot(serverTimestamp, snapshot);
            }
        }

        /// <summary>
        ///     Advances timeout timers and despawns entities that have not received
        ///     a snapshot within <see cref="RemotePlayerEntity.TimeoutSeconds" />.
        ///     Call from Update each frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            _renderTime += deltaTime;

            _timedOutIds.Clear();

            foreach (KeyValuePair<ushort, RemotePlayerEntity> kvp in _entities)
            {
                kvp.Value.TimeoutTimer += deltaTime;

                if (kvp.Value.TimeoutTimer >= RemotePlayerEntity.TimeoutSeconds)
                {
                    _timedOutIds.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _timedOutIds.Count; i++)
            {
                ushort id = _timedOutIds[i];

                if (_entities.TryGetValue(id, out RemotePlayerEntity entity))
                {
                    entity.Dispose();
                    _entities.Remove(id);
                }
            }
        }

        /// <summary>
        ///     Samples interpolation buffers, updates animations, uploads transforms,
        ///     and issues GPU draw calls for all remote player entities.
        ///     Call from LateUpdate after chunk rendering.
        /// </summary>
        public void RenderAll(Camera mainCamera)
        {
            if (_disposed || _entities.Count == 0)
            {
                return;
            }

            Vector3 camPos = mainCamera.transform.position;
            float calibratedRenderTime = _renderTime + _renderTimeOffset;

            foreach (KeyValuePair<ushort, RemotePlayerEntity> kvp in _entities)
            {
                RemotePlayerEntity entity = kvp.Value;

                // Sample interpolation buffer using calibrated render time
                if (!entity.SnapshotBuffer.Sample(
                        calibratedRenderTime, out RemotePlayerSnapshot from, out RemotePlayerSnapshot to, out float alpha))
                {
                    continue;
                }

                // Interpolate position (linear)
                float3 interpPos = math.lerp(from.Position, to.Position, alpha);

                // Interpolate yaw/pitch with angle wrapping to handle ±180° boundary
                float yawDelta = to.Yaw - from.Yaw;
                yawDelta = yawDelta - 360f * math.round(yawDelta / 360f);
                float interpYaw = from.Yaw + yawDelta * alpha;

                float pitchDelta = to.Pitch - from.Pitch;
                pitchDelta = pitchDelta - 360f * math.round(pitchDelta / 360f);
                float interpPitch = from.Pitch + pitchDelta * alpha;

                // Derive flags from the 'to' snapshot (latest known state)
                bool isOnGround = (to.Flags & 0x01) != 0;
                bool isFlying = (to.Flags & 0x02) != 0;

                // Update animator
                entity.Animator.Update(
                    Time.deltaTime, interpPos, interpYaw, interpPitch, isOnGround, isFlying);

                // Upload part transforms
                float4x4[] transforms = entity.Animator.PartTransforms;

                for (int i = 0; i < 6; i++)
                {
                    entity.PartTransformUpload[i] = transforms[i];
                }

                entity.PartTransformsBuffer.SetData(entity.PartTransformUpload);

                // Draw base model layer
                entity.BaseModelParams.matProps.SetBuffer(s_playerVertexBufferId, _sharedVertexBuffer);
                entity.BaseModelParams.matProps.SetBuffer(s_partTransformsId, entity.PartTransformsBuffer);
                entity.BaseModelParams.matProps.SetTexture(s_skinTexId, entity.SkinTexture);

                Graphics.RenderPrimitivesIndexedIndirect(
                    entity.BaseModelParams,
                    MeshTopology.Triangles,
                    _sharedIndexBuffer,
                    _sharedArgsBuffer);

                // Draw overlay model layer
                entity.OverlayModelParams.matProps.SetBuffer(s_playerVertexBufferId, _sharedVertexBuffer);
                entity.OverlayModelParams.matProps.SetBuffer(s_partTransformsId, entity.PartTransformsBuffer);
                entity.OverlayModelParams.matProps.SetTexture(s_skinTexId, entity.SkinTexture);

                Graphics.RenderPrimitivesIndexedIndirect(
                    entity.OverlayModelParams,
                    MeshTopology.Triangles,
                    _sharedIndexBuffer,
                    _sharedArgsBuffer,
                    1,
                    1);

                // Draw name tag (billboard quad facing camera)
                if (entity.NameTagMesh != null)
                {
                    float3 nameTagPos = interpPos + new float3(0f, 2.2f, 0f);
                    Vector3 toCamera = camPos - new Vector3(nameTagPos.x, nameTagPos.y, nameTagPos.z);
                    toCamera.y = 0f;

                    Quaternion billboardRot = toCamera.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(toCamera)
                        : Quaternion.identity;

                    Matrix4x4 nameTagMatrix = Matrix4x4.TRS(
                        new Vector3(nameTagPos.x, nameTagPos.y, nameTagPos.z),
                        billboardRot,
                        Vector3.one);

                    Graphics.DrawMesh(
                        entity.NameTagMesh,
                        nameTagMatrix,
                        _nameTagMaterial,
                        0,
                        null,
                        0,
                        entity.NameTagParams.matProps);
                }
            }
        }
    }
}
