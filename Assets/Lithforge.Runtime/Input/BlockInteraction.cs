using System;
using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Physics;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.Rendering;
using VoxelRaycastHit = Lithforge.Physics.RaycastHit;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
using Lithforge.Voxel.Tag;
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
        private TagRegistry _tagRegistry;
        private Random _lootRandom;

        // Static sort comparison for trait priority ordering
        private static readonly Comparison<IToolTrait> s_traitPrioritySort =
            (IToolTrait a, IToolTrait b) => a.Priority.CompareTo(b.Priority);

        // ToolType → mineable tag mapping (Minecraft-style correct-tool check)
        private static readonly Dictionary<ToolType, ResourceId> s_toolTagMap =
            new Dictionary<ToolType, ResourceId>
            {
                { ToolType.Pickaxe, ResourceId.Parse("lithforge:mineable_pickaxe") },
                { ToolType.Axe, ResourceId.Parse("lithforge:mineable_axe") },
                { ToolType.Shovel, ResourceId.Parse("lithforge:mineable_shovel") },
                { ToolType.Hoe, ResourceId.Parse("lithforge:mineable_hoe") },
                { ToolType.Sword, ResourceId.Parse("lithforge:mineable_sword") },
            };

        // Physics settings
        private float _interactionRange;
        private float _placeCooldownTime;
        private float _handMiningMultiplier;
        private float _toolMiningMultiplier;
        private float _minBreakTime;
        private int _defaultMaxStackSize;
        private float _playerHalfWidth;
        private float _playerHeight;

        // Player transform (feet position)
        private Transform _playerTransform;

        // Player controller reference (for fly mode scroll-wheel gating)
        private PlayerController _playerController;

        // Mining state
        private bool _isMining;
        private int3 _miningBlockCoord;
        private float _miningProgress;
        private float _miningRequiredTime;
        private bool _canHarvest;

        // Placement cooldown
        private float _placeCooldown;

        // Block entity references
        private BlockEntityTickScheduler _blockEntityTickScheduler;
        private ContainerScreenManager _screenManager;

        // Tool system registries
        private ToolTraitRegistry _toolTraitRegistry;

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
            TagRegistry tagRegistry,
            Transform playerTransform,
            PhysicsSettings physics,
            PlayerController playerController)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _stateRegistry = stateRegistry;
            _blockHighlight = blockHighlight;
            _inventory = inventory;
            _itemRegistry = itemRegistry;
            _lootResolver = lootResolver;
            _tagRegistry = tagRegistry;
            _playerTransform = playerTransform;
            _interactionRange = physics.InteractionRange;
            _placeCooldownTime = physics.PlaceCooldownTime;
            _handMiningMultiplier = physics.HandMiningMultiplier;
            _toolMiningMultiplier = physics.ToolMiningMultiplier;
            _minBreakTime = physics.MinBreakTime;
            _defaultMaxStackSize = physics.DefaultMaxStackSize;
            _playerHalfWidth = physics.PlayerHalfWidth;
            _playerHeight = physics.PlayerHeight;
            _playerController = playerController;
            _lootRandom = new Random(Environment.TickCount);
            _isSolidDelegate = SolidBlockQuery.CreateDelegate(_chunkManager, _nativeStateRegistry);
        }

        /// <summary>
        /// Sets block entity system references. Call after ContainerScreenManager is initialized.
        /// </summary>
        public void SetBlockEntityReferences(
            BlockEntityTickScheduler scheduler,
            ContainerScreenManager screenManager,
            ToolTraitRegistry toolTraitRegistry)
        {
            _blockEntityTickScheduler = scheduler;
            _screenManager = screenManager;
            _toolTraitRegistry = toolTraitRegistry;
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

        private static readonly Key[] s_digitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        private void HandleHotbarSelection(Keyboard keyboard)
        {
            for (int i = 0; i < Inventory.HotbarSize && i < s_digitKeys.Length; i++)
            {
                if (keyboard[s_digitKeys[i]].wasPressedThisFrame)
                {
                    _inventory.SelectedSlot = i;

                    return;
                }
            }
        }

        private void HandleScrollWheel(Mouse mouse)
        {
            // In fly mode, scroll wheel controls fly speed instead of hotbar
            if (_playerController != null && _playerController.IsFlying)
            {
                return;
            }

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

            // Check if target changed — skip accumulation on the first frame
            if (!_isMining || !_miningBlockCoord.Equals(hit.BlockCoord))
            {
                StartMining(hit.BlockCoord);

                return;
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
            _canHarvest = true;

            StateId stateId = _chunkManager.GetBlock(blockCoord);
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry != null)
            {
                MiningContext ctx = MiningContext.Default;
                ctx.Hardness = entry.Hardness;
                ctx.Material = entry.MaterialType;
                ctx.RequiredToolLevel = entry.RequiredToolLevel;

                ItemStack heldItem = _inventory.GetSelectedItem();
                IMiningModifier[] mods = null;

                if (!heldItem.IsEmpty)
                {
                    ItemEntry itemDef = _itemRegistry.Get(heldItem.ItemId);

                    if (itemDef != null && itemDef.ToolType != ToolType.None)
                    {
                        ctx.ToolType = itemDef.ToolType;
                        ctx.ToolLevel = itemDef.ToolLevel;

                        if (s_toolTagMap.TryGetValue(itemDef.ToolType, out ResourceId requiredTag))
                        {
                            ctx.IsCorrectTool = _tagRegistry.HasTag(entry.Id, requiredTag);
                        }

                        if (itemDef.ToolSpeedProfile is ToolSpeedProfile profile)
                        {
                            ctx.ToolSpeed = profile.GetSpeed(ctx.Material);
                        }
                        else
                        {
                            ctx.ToolSpeed = itemDef.MiningSpeed;
                        }

                        mods = itemDef.Modifiers;
                    }
                }

                // Apply default harvest denial rules first
                if (entry.RequiredToolLevel > 0 && ctx.ToolLevel < entry.RequiredToolLevel)
                {
                    ctx.CanHarvest = false;
                }

                if (entry.RequiresTool && !ctx.IsCorrectTool)
                {
                    ctx.CanHarvest = false;
                }

                // Apply modifiers after denial checks (GrantHarvest can override)
                if (mods != null)
                {
                    for (int i = 0; i < mods.Length; i++)
                    {
                        ctx = mods[i].Apply(ctx);
                    }
                }

                // Apply modular tool traits (Tinkers-like system)
                if (!heldItem.IsEmpty && heldItem.HasCustomData)
                {
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(heldItem.CustomData);
                    if (tool != null)
                    {
                        ctx.ToolType = tool.ToolType;
                        ctx.ToolSpeed = tool.GetEffectiveSpeed(entry.MaterialType);
                        ctx.ToolLevel = tool.GetEffectiveToolLevel();
                        ctx.SpeedMultiplier *= tool.DurabilityState.GetEffectiveSpeedMultiplier();

                        // Re-evaluate correct tool with modular tool's ToolType
                        if (s_toolTagMap.TryGetValue(tool.ToolType, out ResourceId modularTag))
                        {
                            ctx.IsCorrectTool = _tagRegistry.HasTag(entry.Id, modularTag);
                        }

                        IToolTrait[] traits = tool.GetAllTraits(_toolTraitRegistry);
                        Array.Sort(traits, s_traitPrioritySort);
                        for (int i = 0; i < traits.Length; i++)
                        {
                            ctx = traits[i].Apply(ctx);
                        }
                    }
                }

                float effectiveSpeed = (ctx.ToolSpeed + ctx.FlatSpeedBonus) * ctx.SpeedMultiplier;
                float effectiveHardness = Mathf.Max(0f, ctx.Hardness - ctx.HardnessReduction);

                if (ctx.IsCorrectTool)
                {
                    if (effectiveSpeed <= 0f)
                    {
                        effectiveSpeed = 0.01f;
                    }

                    _miningRequiredTime = effectiveHardness * _toolMiningMultiplier / effectiveSpeed;
                }
                else
                {
                    _miningRequiredTime = effectiveHardness * _handMiningMultiplier;
                }

                _miningRequiredTime = Mathf.Max(_minBreakTime, _miningRequiredTime);
                _canHarvest = ctx.CanHarvest;
            }
            else
            {
                _miningRequiredTime = _minBreakTime;
            }
        }

        private void BreakBlock(int3 blockCoord)
        {
            // Drop block entity inventory items before breaking
            StateId stateId = _chunkManager.GetBlock(blockCoord);

            if (_blockEntityTickScheduler != null &&
                _nativeStateRegistry.States.IsCreated &&
                stateId.Value < _nativeStateRegistry.States.Length &&
                _nativeStateRegistry.States[stateId.Value].HasBlockEntity)
            {
                int3 chunkCoord = new int3(
                    FloorDiv(blockCoord.x, ChunkConstants.Size),
                    FloorDiv(blockCoord.y, ChunkConstants.Size),
                    FloorDiv(blockCoord.z, ChunkConstants.Size));
                int localX = blockCoord.x - chunkCoord.x * ChunkConstants.Size;
                int localY = blockCoord.y - chunkCoord.y * ChunkConstants.Size;
                int localZ = blockCoord.z - chunkCoord.z * ChunkConstants.Size;
                int flatIndex = ChunkData.GetIndex(localX, localY, localZ);

                Lithforge.Runtime.BlockEntity.BlockEntity entity =
                    _blockEntityTickScheduler.GetEntity(chunkCoord, flatIndex);

                if (entity != null)
                {
                    Lithforge.Runtime.BlockEntity.Behaviors.InventoryBehavior entityInv =
                        entity.GetBehavior<Lithforge.Runtime.BlockEntity.Behaviors.InventoryBehavior>();

                    if (entityInv != null)
                    {
                        for (int i = 0; i < entityInv.SlotCount; i++)
                        {
                            ItemStack slot = entityInv.GetSlot(i);

                            if (!slot.IsEmpty)
                            {
                                ItemEntry itemDef = _itemRegistry.Get(slot.ItemId);
                                int maxStack = itemDef != null
                                    ? itemDef.MaxStackSize : _defaultMaxStackSize;
                                _inventory.AddItem(slot.ItemId, slot.Count, maxStack);
                            }
                        }
                    }
                }
            }

            // Resolve loot table before breaking (only if player can harvest this block)
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (_canHarvest && entry != null && !string.IsNullOrEmpty(entry.LootTable))
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
                // Modular tool: consume durability from ToolInstance and re-serialize
                if (heldItem.HasCustomData)
                {
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(heldItem.CustomData);

                    if (tool != null)
                    {
                        tool.CurrentDurability -= 1;

                        if (tool.CurrentDurability <= 0)
                        {
                            _inventory.SetSlot(_inventory.SelectedSlot, ItemStack.Empty);
                        }
                        else
                        {
                            ItemStack updated = heldItem;
                            updated.Durability = tool.CurrentDurability;
                            updated.CustomData = ToolInstanceSerializer.Serialize(tool);
                            _inventory.SetSlot(_inventory.SelectedSlot, updated);
                        }
                    }
                }
                else
                {
                    // Standard tool: consume durability from ItemStack.Durability
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

            // Check if the targeted block has a block entity (right-click to open UI)
            if (_blockEntityTickScheduler != null)
            {
                StateId targetState = _chunkManager.GetBlock(hit.BlockCoord);

                if (_nativeStateRegistry.States.IsCreated &&
                    targetState.Value < _nativeStateRegistry.States.Length &&
                    _nativeStateRegistry.States[targetState.Value].HasBlockEntity)
                {
                    int3 chunkCoord = new int3(
                        FloorDiv(hit.BlockCoord.x, ChunkConstants.Size),
                        FloorDiv(hit.BlockCoord.y, ChunkConstants.Size),
                        FloorDiv(hit.BlockCoord.z, ChunkConstants.Size));
                    int localX = hit.BlockCoord.x - chunkCoord.x * ChunkConstants.Size;
                    int localY = hit.BlockCoord.y - chunkCoord.y * ChunkConstants.Size;
                    int localZ = hit.BlockCoord.z - chunkCoord.z * ChunkConstants.Size;
                    int flatIndex = ChunkData.GetIndex(localX, localY, localZ);

                    Lithforge.Runtime.BlockEntity.BlockEntity entity =
                        _blockEntityTickScheduler.GetEntity(chunkCoord, flatIndex);

                    if (entity != null && _screenManager != null)
                    {
                        if (_screenManager.TryOpenForEntity(entity))
                        {
                            _placeCooldown = _placeCooldownTime;
                            return;
                        }
                    }
                }
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

            // Check that the placed block does not overlap the player AABB
            if (_playerTransform != null)
            {
                float3 feetPos = new float3(
                    _playerTransform.position.x,
                    _playerTransform.position.y,
                    _playerTransform.position.z);

                Aabb playerBox = new Aabb(
                    new float3(feetPos.x - _playerHalfWidth, feetPos.y, feetPos.z - _playerHalfWidth),
                    new float3(feetPos.x + _playerHalfWidth, feetPos.y + _playerHeight, feetPos.z + _playerHalfWidth));

                Aabb blockBox = Aabb.FromBlockCoord(placeCoord);

                if (playerBox.Intersects(blockBox))
                {
                    return;
                }
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

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        private void ResetMining()
        {
            _isMining = false;
            _miningProgress = 0f;
            _miningRequiredTime = 0f;
            _canHarvest = true;
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
