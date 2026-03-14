using System;
using System.Runtime.InteropServices;
using Lithforge.Core.Data;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Item;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Renders first-person arms and held items using GPU-driven indirect draw calls.
    /// Owns all GPU buffers for the arm mesh (static) and held item mesh (rebuilt on change).
    /// Called from GameLoop.LateUpdate after ChunkMeshStore.RenderAll to render on top
    /// of the world via ZTest Always in the arm shaders.
    ///
    /// Owner: GameLoop (via LithforgeBootstrap). Lifetime: application session.
    /// </summary>
    public sealed class FirstPersonArmRenderer : IDisposable
    {
        // Shader property IDs
        private static readonly int s_armVertexBufferId = Shader.PropertyToID("_ArmVertexBuffer");
        private static readonly int s_heldItemVertexBufferId = Shader.PropertyToID("_HeldItemVertexBuffer");
        private static readonly int s_partTransformsId = Shader.PropertyToID("_PartTransforms");
        private static readonly int s_skinTexId = Shader.PropertyToID("_SkinTex");
        private static readonly int s_armToClipId = Shader.PropertyToID("_ArmToClip");

        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds s_worldBounds =
            new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        // GPU buffers — arm mesh (static)
        private readonly GraphicsBuffer _armVertexBuffer;
        private readonly GraphicsBuffer _armIndexBuffer;
        private readonly GraphicsBuffer _armArgsBuffer;

        // GPU buffers — shared per-part transforms (updated per frame)
        private readonly GraphicsBuffer _partTransformsBuffer;
        private readonly Matrix4x4[] _partTransformUpload = new Matrix4x4[6];

        // GPU buffers — held item mesh (rebuilt on item change)
        private GraphicsBuffer _heldItemVertexBuffer;
        private GraphicsBuffer _heldItemIndexBuffer;
        private GraphicsBuffer _heldItemArgsBuffer;
        private bool _hasHeldItemMesh;

        // Render params
        private readonly RenderParams _baseArmParams;
        private readonly RenderParams _overlayArmParams;
        private readonly RenderParams _heldItemParams;

        // Skin texture
        private readonly Texture2D _skinTexture;

        // Animation
        private readonly ArmAnimator _animator;

        // Held item tracking
        private readonly Inventory _inventory;
        private readonly ItemRegistry _itemRegistry;
        private readonly StateRegistry _stateRegistry;
        private ResourceId _lastHeldItemId;
        private bool _hasLastHeldItem;
        private int _lastSelectedSlot = -1;

        // FOV for arm projection
        private const float ArmFov = 70f;
        private const float ArmNearClip = 0.05f;
        private const float ArmFarClip = 10f;

        public FirstPersonArmRenderer(
            Material armBaseMaterial,
            Material armOverlayMaterial,
            Material heldItemMaterial,
            Texture2D skinTexture,
            Transform playerTransform,
            Inventory inventory,
            ItemRegistry itemRegistry,
            StateRegistry stateRegistry)
        {
            _skinTexture = skinTexture;
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _stateRegistry = stateRegistry;
            _animator = new ArmAnimator(playerTransform);

            // Build arm mesh (static geometry)
            PlayerArmMeshBuilder.Build(false, out PlayerArmVertex[] armVertices, out int[] armIndices);

            // Create GPU buffers for arm mesh
            _armVertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                PlayerArmMeshBuilder.TotalVertexCount,
                Marshal.SizeOf<PlayerArmVertex>());
            _armVertexBuffer.SetData(armVertices);

            _armIndexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                PlayerArmMeshBuilder.TotalIndexCount,
                sizeof(int));
            _armIndexBuffer.SetData(armIndices);

            // Create indirect args buffer for arm (2 draw commands: base + overlay)
            _armArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                2,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            GraphicsBuffer.IndirectDrawIndexedArgs[] armArgs = new GraphicsBuffer.IndirectDrawIndexedArgs[2];

            // Base layer: right arm base (indices 0-35) + left arm base (indices 36-71)
            armArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = (uint)PlayerArmMeshBuilder.BaseLayerIndexCount,
                instanceCount = 1,
                startIndex = 0,
                baseVertexIndex = 0,
                startInstance = 0,
            };

            // Overlay layer: right arm overlay (indices 72-107) + left arm overlay (indices 108-143)
            armArgs[1] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = (uint)PlayerArmMeshBuilder.OverlayLayerIndexCount,
                instanceCount = 1,
                startIndex = (uint)PlayerArmMeshBuilder.BaseLayerIndexCount,
                baseVertexIndex = 0,
                startInstance = 0,
            };
            _armArgsBuffer.SetData(armArgs);

            // Per-part transform buffer (6 matrices)
            _partTransformsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                6,
                64); // sizeof(float4x4) = 64

            for (int i = 0; i < 6; i++)
            {
                _partTransformUpload[i] = Matrix4x4.identity;
            }
            _partTransformsBuffer.SetData(_partTransformUpload);

            // Build RenderParams for arm base layer
            _baseArmParams = new RenderParams(armBaseMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };

            // Build RenderParams for arm overlay layer
            _overlayArmParams = new RenderParams(armOverlayMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };

            // Build RenderParams for held item
            _heldItemParams = new RenderParams(heldItemMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
        }

        /// <summary>
        /// Main render call. Must be called from LateUpdate after ChunkMeshStore.RenderAll.
        /// Updates animations and issues draw calls for arm + held item.
        /// </summary>
        public void Render(Camera camera, bool isOnGround, bool isFlying, bool isMining)
        {
            if (camera == null)
            {
                return;
            }

            // Check for held item changes and trigger equip animation
            UpdateHeldItem();

            // Update animations
            _animator.Update(Time.deltaTime, isMining, isOnGround, isFlying);

            // Upload part transforms
            float4x4[] transforms = _animator.PartTransforms;

            for (int i = 0; i < 6; i++)
            {
                _partTransformUpload[i] = transforms[i];
            }
            _partTransformsBuffer.SetData(_partTransformUpload);

            // Compute arm projection matrix (separate FOV from world camera)
            Matrix4x4 armProjection = Matrix4x4.Perspective(ArmFov, camera.aspect, ArmNearClip, ArmFarClip);
            armProjection = GL.GetGPUProjectionMatrix(armProjection, true);
            Matrix4x4 armToClip = armProjection * camera.worldToCameraMatrix;

            // Draw arm base layer
            _baseArmParams.matProps.SetBuffer(s_armVertexBufferId, _armVertexBuffer);
            _baseArmParams.matProps.SetBuffer(s_partTransformsId, _partTransformsBuffer);
            _baseArmParams.matProps.SetTexture(s_skinTexId, _skinTexture);
            _baseArmParams.matProps.SetMatrix(s_armToClipId, armToClip);

            Graphics.RenderPrimitivesIndexedIndirect(
                _baseArmParams,
                MeshTopology.Triangles,
                _armIndexBuffer,
                _armArgsBuffer,
                1,
                0);

            // Draw arm overlay layer
            _overlayArmParams.matProps.SetBuffer(s_armVertexBufferId, _armVertexBuffer);
            _overlayArmParams.matProps.SetBuffer(s_partTransformsId, _partTransformsBuffer);
            _overlayArmParams.matProps.SetTexture(s_skinTexId, _skinTexture);
            _overlayArmParams.matProps.SetMatrix(s_armToClipId, armToClip);

            Graphics.RenderPrimitivesIndexedIndirect(
                _overlayArmParams,
                MeshTopology.Triangles,
                _armIndexBuffer,
                _armArgsBuffer,
                1,
                1);

            // Draw held item (if any)
            if (_hasHeldItemMesh)
            {
                _heldItemParams.matProps.SetBuffer(s_heldItemVertexBufferId, _heldItemVertexBuffer);
                _heldItemParams.matProps.SetBuffer(s_partTransformsId, _partTransformsBuffer);
                _heldItemParams.matProps.SetMatrix(s_armToClipId, armToClip);

                Graphics.RenderPrimitivesIndexedIndirect(
                    _heldItemParams,
                    MeshTopology.Triangles,
                    _heldItemIndexBuffer,
                    _heldItemArgsBuffer,
                    1,
                    0);
            }
        }

        /// <summary>
        /// Checks if the held item changed and rebuilds the held item mesh if needed.
        /// </summary>
        private void UpdateHeldItem()
        {
            int selectedSlot = _inventory.SelectedSlot;
            ItemStack stack = _inventory.GetSelectedItem();
            bool hasItem = !stack.IsEmpty;
            ResourceId currentId = hasItem ? stack.ItemId : default;

            bool slotChanged = selectedSlot != _lastSelectedSlot;
            bool itemChanged = hasItem != _hasLastHeldItem ||
                (hasItem && !currentId.Equals(_lastHeldItemId));

            if (!slotChanged && !itemChanged)
            {
                return;
            }

            _lastSelectedSlot = selectedSlot;

            if (itemChanged)
            {
                _lastHeldItemId = currentId;
                _hasLastHeldItem = hasItem;
                _animator.TriggerEquip();
                RebuildHeldItemMesh(hasItem ? currentId : default);
            }
        }

        /// <summary>
        /// Rebuilds the GPU buffers for the held item mesh.
        /// </summary>
        private void RebuildHeldItemMesh(ResourceId itemId)
        {
            // Dispose old held item buffers
            DisposeHeldItemBuffers();

            if (itemId.Namespace == null)
            {
                _hasHeldItemMesh = false;
                return;
            }

            ItemEntry itemEntry = _itemRegistry.Get(itemId);

            if (itemEntry == null)
            {
                _hasHeldItemMesh = false;
                return;
            }

            HeldItemVertex[] vertices;
            int[] indices;

            if (itemEntry.IsBlockItem)
            {
                HeldItemMeshBuilder.BuildBlockItem(
                    _stateRegistry,
                    itemEntry.BlockId,
                    out vertices,
                    out indices);
            }
            else
            {
                HeldItemMeshBuilder.BuildFlatItem(out vertices, out indices);
            }

            if (vertices.Length == 0 || indices.Length == 0)
            {
                _hasHeldItemMesh = false;
                return;
            }

            // Create GPU buffers
            _heldItemVertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                vertices.Length,
                Marshal.SizeOf<HeldItemVertex>());
            _heldItemVertexBuffer.SetData(vertices);

            _heldItemIndexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                indices.Length,
                sizeof(int));
            _heldItemIndexBuffer.SetData(indices);

            _heldItemArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            GraphicsBuffer.IndirectDrawIndexedArgs[] args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            args[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = (uint)indices.Length,
                instanceCount = 1,
                startIndex = 0,
                baseVertexIndex = 0,
                startInstance = 0,
            };
            _heldItemArgsBuffer.SetData(args);

            _hasHeldItemMesh = true;
        }

        private void DisposeHeldItemBuffers()
        {
            _heldItemVertexBuffer?.Dispose();
            _heldItemVertexBuffer = null;
            _heldItemIndexBuffer?.Dispose();
            _heldItemIndexBuffer = null;
            _heldItemArgsBuffer?.Dispose();
            _heldItemArgsBuffer = null;
            _hasHeldItemMesh = false;
        }

        public void Dispose()
        {
            _armVertexBuffer?.Dispose();
            _armIndexBuffer?.Dispose();
            _armArgsBuffer?.Dispose();
            _partTransformsBuffer?.Dispose();
            DisposeHeldItemBuffers();

            if (_skinTexture != null)
            {
                UnityEngine.Object.Destroy(_skinTexture);
            }
        }
    }
}
