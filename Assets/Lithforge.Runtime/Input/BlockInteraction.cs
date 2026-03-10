using System;
using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Physics;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using VoxelRaycastHit = Lithforge.Physics.RaycastHit;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

using Random = System.Random;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Handles block breaking (left click, progressive mining) and
    /// block placement (right click) via voxel raycasting.
    /// Integrates with inventory for item pickup and block placement.
    /// Attached to the camera GameObject.
    /// </summary>
    public sealed class BlockInteraction : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private NativeStateRegistry _nativeStateRegistry;
        private StateRegistry _stateRegistry;
        private BlockHighlight _blockHighlight;
        private Func<int3, bool> _isSolidDelegate;

        // Inventory integration
        private Inventory _inventory;
        private ItemRegistry _itemRegistry;
        private LootResolver _lootResolver;
        private Random _lootRandom;

        // Physics settings
        private float _interactionRange;
        private float _placeCooldownTime;
        private float _handMiningMultiplier;
        private float _toolMiningMultiplier;
        private float _minBreakTime;
        private int _defaultMaxStackSize;

        // Mining state
        private bool _isMining;
        private int3 _miningBlockCoord;
        private float _miningProgress;
        private float _miningRequiredTime;

        // Placement cooldown
        private float _placeCooldown;

        // Reusable list for dirty chunks
        private readonly List<int3> _dirtiedChunks = new List<int3>();


        public void Initialize(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            StateRegistry stateRegistry,
            BlockHighlight blockHighlight,
            Inventory inventory,
            ItemRegistry itemRegistry,
            LootResolver lootResolver,
            PhysicsSettings physics)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _stateRegistry = stateRegistry;
            _blockHighlight = blockHighlight;
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _lootResolver = lootResolver;
            _interactionRange = physics.InteractionRange;
            _placeCooldownTime = physics.PlaceCooldownTime;
            _handMiningMultiplier = physics.HandMiningMultiplier;
            _toolMiningMultiplier = physics.ToolMiningMultiplier;
            _minBreakTime = physics.MinBreakTime;
            _defaultMaxStackSize = physics.DefaultMaxStackSize;
            _lootRandom = new Random(Environment.TickCount);
            _isSolidDelegate = (int3 coord) => SolidBlockQuery.IsSolid(
                coord, _chunkManager, _nativeStateRegistry);
        }

        private void Update()
        {
            if (_chunkManager == null || Cursor.lockState != CursorLockMode.Locked)
            {
                if (_blockHighlight != null)
                {
                    _blockHighlight.Hide();
                }

                return;
            }

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse == null)
            {
                return;
            }

            // Handle hotbar selection
            if (keyboard != null)
            {
                HandleHotbarSelection(keyboard);
            }

            HandleScrollWheel(mouse);

            // Update placement cooldown
            if (_placeCooldown > 0f)
            {
                _placeCooldown -= Time.deltaTime;
            }

            // Cast ray from camera
            VoxelRaycastHit hit = VoxelRaycast.Cast(
                new float3(transform.position.x, transform.position.y, transform.position.z),
                new float3(transform.forward.x, transform.forward.y, transform.forward.z),
                _interactionRange,
                _isSolidDelegate);

            if (hit.DidHit)
            {
                _blockHighlight.SetTarget(hit.BlockCoord);
                HandleMining(mouse, hit);
                HandlePlacement(mouse, hit);
            }
            else
            {
                _blockHighlight.Hide();
                ResetMining();
            }
        }

        private static readonly Key[] _digitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        private void HandleHotbarSelection(Keyboard keyboard)
        {
            for (int i = 0; i < Inventory.HotbarSize && i < _digitKeys.Length; i++)
            {
                if (keyboard[_digitKeys[i]].wasPressedThisFrame)
                {
                    _inventory.SelectedSlot = i;

                    return;
                }
            }
        }

        private void HandleScrollWheel(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;

            if (scroll > 0.1f)
            {
                int slot = _inventory.SelectedSlot - 1;

                if (slot < 0)
                {
                    slot = Inventory.HotbarSize - 1;
                }

                _inventory.SelectedSlot = slot;
            }
            else if (scroll < -0.1f)
            {
                int slot = _inventory.SelectedSlot + 1;

                if (slot >= Inventory.HotbarSize)
                {
                    slot = 0;
                }

                _inventory.SelectedSlot = slot;
            }
        }

        private void HandleMining(Mouse mouse, VoxelRaycastHit hit)
        {
            if (!mouse.leftButton.isPressed)
            {
                ResetMining();

                return;
            }

            // Check if target changed
            if (!_isMining || !_miningBlockCoord.Equals(hit.BlockCoord))
            {
                StartMining(hit.BlockCoord);
            }

            // Accumulate mining progress
            _miningProgress += Time.deltaTime;

            if (_miningProgress >= _miningRequiredTime)
            {
                BreakBlock(hit.BlockCoord);
                ResetMining();
            }
        }

        private void StartMining(int3 blockCoord)
        {
            _isMining = true;
            _miningBlockCoord = blockCoord;
            _miningProgress = 0f;

            // Look up hardness from StateRegistryEntry
            StateId stateId = _chunkManager.GetBlock(blockCoord);
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry != null)
            {
                // Check if held item is the correct tool
                float toolMultiplier = _handMiningMultiplier;
                ItemStack heldItem = _inventory.GetSelectedItem();

                if (!heldItem.IsEmpty)
                {
                    ItemEntry itemDef = _itemRegistry.Get(heldItem.ItemId);

                    if (itemDef != null && itemDef.ToolType != ToolType.None)
                    {
                        toolMultiplier = _toolMiningMultiplier;
                        // Mining speed modifier from tool
                        float miningSpeed = itemDef.MiningSpeed;

                        if (miningSpeed > 1.0f)
                        {
                            toolMultiplier /= miningSpeed;
                        }
                    }
                }

                _miningRequiredTime = entry.Hardness * toolMultiplier;

                // Minimum break time (instant break for hardness 0)
                if (_miningRequiredTime < _minBreakTime)
                {
                    _miningRequiredTime = _minBreakTime;
                }
            }
            else
            {
                _miningRequiredTime = _minBreakTime;
            }
        }

        private void BreakBlock(int3 blockCoord)
        {
            // Resolve loot table before breaking
            StateId stateId = _chunkManager.GetBlock(blockCoord);
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry != null && !string.IsNullOrEmpty(entry.LootTable))
            {
                if (ResourceId.TryParse(entry.LootTable, out ResourceId tableId))
                {
                    List<LootDrop> drops = _lootResolver.Resolve(tableId, _lootRandom);

                    for (int i = 0; i < drops.Count; i++)
                    {
                        LootDrop drop = drops[i];
                        ItemEntry itemDef = _itemRegistry.Get(drop.ItemId);
                        int maxStack = itemDef != null ? itemDef.MaxStackSize : _defaultMaxStackSize;

                        _inventory.AddItem(drop.ItemId, drop.Count, maxStack);
                    }
                }
            }

            // Consume durability of held tool
            ItemStack heldItem = _inventory.GetSelectedItem();

            if (!heldItem.IsEmpty)
            {
                ItemEntry heldDef = _itemRegistry.Get(heldItem.ItemId);

                if (heldDef != null && heldDef.Durability > 0 && heldItem.Durability != -1)
                {
                    ItemStack updated = heldItem;
                    updated.Durability -= 1;

                    if (updated.Durability <= 0)
                    {
                        _inventory.SetSlot(_inventory.SelectedSlot, ItemStack.Empty);
                    }
                    else
                    {
                        _inventory.SetSlot(_inventory.SelectedSlot, updated);
                    }
                }
            }

            _dirtiedChunks.Clear();
            _chunkManager.SetBlock(blockCoord, StateId.Air, _dirtiedChunks);
        }

        private void HandlePlacement(Mouse mouse, VoxelRaycastHit hit)
        {
            if (!mouse.rightButton.wasPressedThisFrame || _placeCooldown > 0f)
            {
                return;
            }

            // Get block to place from selected inventory slot
            ItemStack selectedItem = _inventory.GetSelectedItem();

            if (selectedItem.IsEmpty)
            {
                return;
            }

            ItemEntry itemDef = _itemRegistry.Get(selectedItem.ItemId);

            if (itemDef == null || !itemDef.IsBlockItem)
            {
                return;
            }

            // Find the StateId for this block
            StateId placeState = FindStateIdForBlock(itemDef.BlockId);

            if (placeState == StateId.Air)
            {
                return;
            }

            // Place on the adjacent face
            int3 placeCoord = hit.BlockCoord + hit.Normal;

            // Check the target position is air
            StateId existing = _chunkManager.GetBlock(placeCoord);

            if (existing != StateId.Air)
            {
                return;
            }

            _dirtiedChunks.Clear();
            _chunkManager.SetBlock(placeCoord, placeState, _dirtiedChunks);

            // Consume one item from inventory
            _inventory.RemoveFromSlot(_inventory.SelectedSlot, 1);

            _placeCooldown = _placeCooldownTime;
        }

        private StateId FindStateIdForBlock(ResourceId blockId)
        {
            System.Collections.Generic.IReadOnlyList<StateRegistryEntry> entries =
                _stateRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (entry.Id == blockId)
                {
                    return new StateId(entry.BaseStateId);
                }
            }

            return StateId.Air;
        }

        private void ResetMining()
        {
            _isMining = false;
            _miningProgress = 0f;
            _miningRequiredTime = 0f;
        }


        /// <summary>
        /// Gets the current mining progress as a value from 0 to 1.
        /// </summary>
        public float MiningProgress
        {
            get
            {
                if (!_isMining || _miningRequiredTime <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(_miningProgress / _miningRequiredTime);
            }
        }

        /// <summary>
        /// True if the player is currently mining a block.
        /// </summary>
        public bool IsMining
        {
            get { return _isMining; }
        }

        /// <summary>
        /// The player inventory managed by this interaction controller.
        /// </summary>
        public Inventory Inventory
        {
            get { return _inventory; }
        }
    }
}
