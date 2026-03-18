using System;
using System.Runtime.InteropServices;
using Lithforge.Core.Data;
using Lithforge.Voxel.Block;
using Lithforge.Item;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    /// Renders the full player model (body, arms, legs, head) and held items using
    /// GPU-driven indirect draw calls. In first-person mode the head is excluded by
    /// reducing the index count (head indices are at the end of each layer).
    ///
    /// The model is rendered in world space: body anchored at playerTransform.position,
    /// rotated by camera yaw. Shaders use UNITY_MATRIX_VP so the model renders correctly
    /// in any camera (game, scene view, future multiplayer spectators).
    ///
    /// Owner: GameLoop (via LithforgeBootstrap). Lifetime: application session.
    /// </summary>
    public sealed class PlayerRenderer : IDisposable
    {
        // Shader property IDs
        private static readonly int s_playerVertexBufferId = Shader.PropertyToID("_PlayerVertexBuffer");
        private static readonly int s_heldItemVertexBufferId = Shader.PropertyToID("_HeldItemVertexBuffer");
        private static readonly int s_partTransformsId = Shader.PropertyToID("_PartTransforms");
        private static readonly int s_skinTexId = Shader.PropertyToID("_SkinTex");
        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds s_worldBounds = new(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        // GPU buffers — player model mesh (static)
        private readonly GraphicsBuffer _playerVertexBuffer;
        private readonly GraphicsBuffer _playerIndexBuffer;
        private readonly GraphicsBuffer _playerArgsBuffer;

        // GPU buffers — shared per-part transforms (updated per frame)
        private readonly GraphicsBuffer _partTransformsBuffer;
        private readonly Matrix4x4[] _partTransformUpload = new Matrix4x4[6];

        // GPU buffers — held item mesh (rebuilt on item change)
        private GraphicsBuffer _heldItemVertexBuffer;
        private GraphicsBuffer _heldItemIndexBuffer;
        private GraphicsBuffer _heldItemArgsBuffer;
        private bool _hasHeldItemMesh;

        // Render params
        private RenderParams _baseModelParams;
        private RenderParams _overlayModelParams;
        private RenderParams _heldItemParams;

        // Skin texture
        private readonly Texture2D _skinTexture;

        // Animation
        private readonly PlayerModelAnimator _animator;

        // Held item tracking
        private readonly Inventory _inventory;
        private readonly ItemRegistry _itemRegistry;
        private readonly StateRegistry _stateRegistry;
        private readonly ItemDisplayTransformLookup _displayTransformLookup;
        private ResourceId _lastHeldItemId;
        private bool _hasLastHeldItem;
        private int _lastSelectedSlot = -1;

        public PlayerRenderer(
            Material modelBaseMaterial,
            Material modelOverlayMaterial,
            Material heldItemMaterial,
            Texture2D skinTexture,
            Transform playerTransform,
            Transform cameraTransform,
            Inventory inventory,
            ItemRegistry itemRegistry,
            StateRegistry stateRegistry,
            ItemDisplayTransformLookup displayTransformLookup)
        {
            _skinTexture = skinTexture;
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _stateRegistry = stateRegistry;
            _displayTransformLookup = displayTransformLookup;
            _animator = new PlayerModelAnimator(playerTransform, cameraTransform);

            // Build player model mesh (static geometry)
            PlayerModelMeshBuilder.Build(false, out PlayerModelVertex[] modelVertices, out int[] modelIndices);

            // Create GPU buffers for player model mesh
            _playerVertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                PlayerModelMeshBuilder.TotalVertexCount,
                Marshal.SizeOf<PlayerModelVertex>());
            _playerVertexBuffer.SetData(modelVertices);

            _playerIndexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index,
                PlayerModelMeshBuilder.TotalIndexCount,
                sizeof(int));
            _playerIndexBuffer.SetData(modelIndices);

            // Create indirect args buffer for player model (2 draw commands: base + overlay)
            _playerArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                2,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            GraphicsBuffer.IndirectDrawIndexedArgs[] modelArgs = new GraphicsBuffer.IndirectDrawIndexedArgs[2];

            // Base layer: all 6 parts including head (216 indices).
            // Head is naturally invisible from inside (Cull Back) — camera is at y=1.62,
            // head spans y=[1.5, 2.0], so the camera sits inside the head box.
            modelArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = (uint)PlayerModelMeshBuilder.ThirdPersonLayerIndexCount,
                instanceCount = 1,
                startIndex = 0,
                baseVertexIndex = 0,
                startInstance = 0,
            };

            // Overlay layer: all 6 parts including hat (216 indices)
            modelArgs[1] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = (uint)PlayerModelMeshBuilder.ThirdPersonLayerIndexCount,
                instanceCount = 1,
                startIndex = (uint)PlayerModelMeshBuilder.ThirdPersonLayerIndexCount,
                baseVertexIndex = 0,
                startInstance = 0,
            };
            _playerArgsBuffer.SetData(modelArgs);

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

            // Build RenderParams for model base layer
            _baseModelParams = new RenderParams(modelBaseMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = s_worldBounds,
                matProps = new MaterialPropertyBlock(),
            };

            // Build RenderParams for model overlay layer
            _overlayModelParams = new RenderParams(modelOverlayMaterial)
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
        /// Updates animations and issues draw calls for player model + held item.
        /// </summary>
        public void Render(bool isOnGround, bool isFlying, bool isMining)
        {
            // Check for held item changes and trigger equip animation
            UpdateHeldItem();

            // Update animations
            _animator.Update(Time.deltaTime, isMining, isOnGround, isFlying);

            // Upload part transforms (world-space)
            float4x4[] transforms = _animator.PartTransforms;

            for (int i = 0; i < 6; i++)
            {
                _partTransformUpload[i] = transforms[i];
            }
            _partTransformsBuffer.SetData(_partTransformUpload);

            // Draw model base layer
            _baseModelParams.matProps.SetBuffer(s_playerVertexBufferId, _playerVertexBuffer);
            _baseModelParams.matProps.SetBuffer(s_partTransformsId, _partTransformsBuffer);
            _baseModelParams.matProps.SetTexture(s_skinTexId, _skinTexture);

            Graphics.RenderPrimitivesIndexedIndirect(
                _baseModelParams,
                MeshTopology.Triangles,
                _playerIndexBuffer,
                _playerArgsBuffer,
                1,
                0);

            // Draw model overlay layer
            _overlayModelParams.matProps.SetBuffer(s_playerVertexBufferId, _playerVertexBuffer);
            _overlayModelParams.matProps.SetBuffer(s_partTransformsId, _partTransformsBuffer);
            _overlayModelParams.matProps.SetTexture(s_skinTexId, _skinTexture);

            Graphics.RenderPrimitivesIndexedIndirect(
                _overlayModelParams,
                MeshTopology.Triangles,
                _playerIndexBuffer,
                _playerArgsBuffer,
                1,
                1);

            // Draw held item (if any)
            if (_hasHeldItemMesh)
            {
                _heldItemParams.matProps.SetBuffer(s_heldItemVertexBufferId, _heldItemVertexBuffer);
                _heldItemParams.matProps.SetBuffer(s_partTransformsId, _partTransformsBuffer);

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
            float4x4 displayMatrix = _displayTransformLookup.Get(itemId);

            if (itemEntry.IsBlockItem)
            {
                HeldItemMeshBuilder.BuildBlockItem(
                    _stateRegistry,
                    itemEntry.BlockId,
                    displayMatrix,
                    out vertices,
                    out indices);
            }
            else
            {
                HeldItemMeshBuilder.BuildFlatItem(displayMatrix, out vertices, out indices);
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
            _playerVertexBuffer?.Dispose();
            _playerIndexBuffer?.Dispose();
            _playerArgsBuffer?.Dispose();
            _partTransformsBuffer?.Dispose();
            DisposeHeldItemBuffers();

            if (_skinTexture != null)
            {
                UnityEngine.Object.Destroy(_skinTexture);
            }
        }
    }
}
