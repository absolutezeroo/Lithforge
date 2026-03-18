using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Physics;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;
using Lithforge.Item;
using Lithforge.Item.Loot;
using Lithforge.Voxel.Tag;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.InputSystem;

using VoxelRaycastHit = Lithforge.Physics.RaycastHit;
using Random = System.Random;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    ///     Handles block breaking (left click, progressive mining) and
    ///     block placement (right click) via voxel raycasting.
    ///     Integrates with inventory for item pickup and block placement.
    ///     Attached to the camera GameObject.
    /// </summary>
    public sealed class BlockInteraction : MonoBehaviour
    {
        // Static sort comparison for trait priority ordering
        private static readonly Comparison<IToolTrait> s_traitPrioritySort =
            (a, b) => a.Priority.CompareTo(b.Priority);

        // ToolType → mineable tag mapping (Minecraft-style correct-tool check)
        // Tag names must match the tagName field in Tag assets (e.g. "blocks/mineable_pickaxe")
        private static readonly Dictionary<ToolType, ResourceId> s_toolTagMap =
            new()
            {
                {
                    ToolType.Pickaxe, ResourceId.Parse("lithforge:blocks/mineable_pickaxe")
                },
                {
                    ToolType.Axe, ResourceId.Parse("lithforge:blocks/mineable_axe")
                },
                {
                    ToolType.Shovel, ResourceId.Parse("lithforge:blocks/mineable_shovel")
                },
            };

        private static readonly Key[] s_digitKeys =
        {
            Key.Digit1,
            Key.Digit2,
            Key.Digit3,
            Key.Digit4,
            Key.Digit5,
            Key.Digit6,
            Key.Digit7,
            Key.Digit8,
            Key.Digit9,
        };

        // Reusable list for dirty chunks
        private readonly List<int3> _dirtiedChunks = new();

        // Block entity references
        private BlockEntityTickScheduler _blockEntityTickScheduler;
        private BlockHighlight _blockHighlight;

        // Audio
        private BlockSoundPlayer _blockSoundPlayer;
        private bool _canHarvest;
        private ChunkManager _chunkManager;

        // Command processor (delegates validation + state mutation to LocalCommandProcessor)
        private ICommandProcessor _commandProcessor;
        private int _defaultMaxStackSize;
        private float _handMiningMultiplier;

        // Physics settings
        private float _interactionRange;

        // Inventory integration

        // Mining state
        private Func<int3, bool> _isSolidDelegate;
        private ItemRegistry _itemRegistry;
        private Random _lootRandom;
        private LootResolver _lootResolver;
        private float _minBreakTime;
        private int3 _miningBlockCoord;
        private int _miningHitInterval = 4;
        private int _miningHitTickCounter;
        private float _miningProgress;
        private float _miningRequiredTime;
        private NativeStateRegistry _nativeStateRegistry;

        // Network callback: notifies server when mining begins (for break speed validation)
        private Action<int3> _onStartDigging;

        // Placement cooldown
        private float _placeCooldown;
        private float _placeCooldownTime;

        // Player controller reference (for fly mode scroll-wheel gating)
        private PlayerController _playerController;
        private float _playerHalfWidth;
        private float _playerHeight;

        // Player transform (feet position)
        private Transform _playerTransform;
        private ContainerScreenManager _screenManager;
        private StateRegistry _stateRegistry;
        private TagRegistry _tagRegistry;
        private float _toolMiningMultiplier;

        // Tool system registries
        private ToolTraitRegistry _toolTraitRegistry;


        /// <summary>
        ///     Gets the current mining progress as a value from 0 to 1.
        /// </summary>
        public float MiningProgress
        {
            get
            {
                if (!IsMining || _miningRequiredTime <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(_miningProgress / _miningRequiredTime);
            }
        }

        /// <summary>
        ///     True if the player is currently mining a block.
        /// </summary>
        public bool IsMining { get; private set; }

        /// <summary>
        ///     The player inventory managed by this interaction controller.
        /// </summary>
        public Inventory Inventory { get; private set; }

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

        /// <summary>
        ///     Sets the command processor used for block placement and break validation.
        ///     When null, falls back to direct ChunkManager calls.
        /// </summary>
        public void SetCommandProcessor(ICommandProcessor processor)
        {
            _commandProcessor = processor;
        }

        /// <summary>
        ///     Sets a callback invoked when the player begins mining a new block.
        ///     In network mode, this sends a StartDiggingCmd message to the server.
        /// </summary>
        public void SetStartDiggingCallback(Action<int3> callback)
        {
            _onStartDigging = callback;
        }

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
            Inventory = inventory;
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
            _isSolidDelegate = SolidBlockHelper.CreateDelegate(_chunkManager, _nativeStateRegistry);
        }

        /// <summary>
        ///     Sets block entity system references. Call after ContainerScreenManager is initialized.
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

        /// <summary>
        ///     Sets the block sound player for break/place/hit audio.
        /// </summary>
        public void SetBlockSoundPlayer(BlockSoundPlayer player, int miningHitInterval)
        {
            _blockSoundPlayer = player;
            _miningHitInterval = miningHitInterval;
        }

        private void HandleHotbarSelection(Keyboard keyboard)
        {
            for (int i = 0; i < Inventory.HotbarSize && i < s_digitKeys.Length; i++)
            {
                if (keyboard[s_digitKeys[i]].wasPressedThisFrame)
                {
                    Inventory.SelectedSlot = i;

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
                int slot = Inventory.SelectedSlot - 1;

                if (slot < 0)
                {
                    slot = Inventory.HotbarSize - 1;
                }

                Inventory.SelectedSlot = slot;
            }
            else if (scroll < -0.1f)
            {
                int slot = Inventory.SelectedSlot + 1;

                if (slot >= Inventory.HotbarSize)
                {
                    slot = 0;
                }

                Inventory.SelectedSlot = slot;
            }
        }

        private void HandleMining(Mouse mouse, VoxelRaycastHit hit)
        {
            if (!mouse.leftButton.isPressed)
            {
                ResetMining();

                return;
            }

            // Check if target changed — reset mining on new block
            if (!IsMining || !_miningBlockCoord.Equals(hit.BlockCoord))
            {
                StartMining(hit.BlockCoord);
            }

            // Mining progress accumulation happens in TickMining() at fixed tick rate
        }

        /// <summary>
        ///     Accumulates mining progress at fixed tick rate. Called by MiningTickAdapter.
        /// </summary>
        public void TickMining(float tickDt)
        {
            if (!IsMining)
            {
                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null || !mouse.leftButton.isPressed)
            {
                return;
            }

            _miningProgress += tickDt;

            // Play mining hit sound at regular tick intervals
            _miningHitTickCounter++;

            if (_miningHitTickCounter >= _miningHitInterval && _blockSoundPlayer != null)
            {
                _miningHitTickCounter = 0;
                StateId hitState = _chunkManager.GetBlock(_miningBlockCoord);
                _blockSoundPlayer.PlayBlockSound(hitState, SoundEventType.Hit, _miningBlockCoord);
            }

            if (_miningProgress >= _miningRequiredTime)
            {
                BreakBlock(_miningBlockCoord);
                ResetMining();
            }
        }

        private void StartMining(int3 blockCoord)
        {
            IsMining = true;
            _miningBlockCoord = blockCoord;
            _miningProgress = 0f;
            _miningHitTickCounter = 0;
            _canHarvest = true;

            StateId stateId = _chunkManager.GetBlock(blockCoord);
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry != null)
            {
                MiningContext ctx = MiningContext.Default;
                ctx.Hardness = entry.Hardness;
                ctx.Material = entry.MaterialType;
                ctx.RequiredToolLevel = entry.RequiredToolLevel;

                ItemStack heldItem = Inventory.GetSelectedItem();
                ToolInstance tool = null;

                if (!heldItem.IsEmpty && heldItem.HasComponents)
                {
                    ToolInstanceComponent toolComp = heldItem.Components.Get<ToolInstanceComponent>(
                        DataComponentTypes.ToolInstanceId);

                    if (toolComp != null)
                    {
                        tool = toolComp.Tool;
                    }
                }

                // Broken tools act as bare hand
                if (tool != null && tool.IsBroken)
                {
                    tool = null;
                }

                // Apply modular tool traits
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

                    // Apply default harvest denial rules
                    if (entry.RequiredToolLevel > 0 && ctx.ToolLevel < entry.RequiredToolLevel)
                    {
                        ctx.CanHarvest = false;
                    }

                    if (entry.RequiresTool && !ctx.IsCorrectTool)
                    {
                        ctx.CanHarvest = false;
                    }

                    // Apply traits after denial checks (GrantHarvest can override)
                    IToolTrait[] traits = tool.GetAllTraits(_toolTraitRegistry);

                    Array.Sort(traits, s_traitPrioritySort);

                    for (int i = 0; i < traits.Length; i++)
                    {
                        ctx = traits[i].Apply(ctx);
                    }
                }
                else
                {
                    // Bare-hand / non-tool / deserialization failure: apply denial
                    if (entry.RequiredToolLevel > 0)
                    {
                        ctx.CanHarvest = false;
                    }

                    if (entry.RequiresTool)
                    {
                        ctx.CanHarvest = false;
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

            // Notify server (network mode) that mining has started
            _onStartDigging?.Invoke(blockCoord);
        }

        private void BreakBlock(int3 blockCoord)
        {
            // Play break sound before the block is replaced with air
            StateId stateId = _chunkManager.GetBlock(blockCoord);

            if (_blockSoundPlayer != null)
            {
                _blockSoundPlayer.PlayBlockSound(stateId, SoundEventType.Break, blockCoord);
            }

            if (_blockEntityTickScheduler != null &&
                _nativeStateRegistry.States.IsCreated &&
                stateId.Value < _nativeStateRegistry.States.Length &&
                _nativeStateRegistry.States[stateId.Value].HasBlockEntity)
            {
                int3 chunkCoord = new(
                    FloorDiv(blockCoord.x, ChunkConstants.Size),
                    FloorDiv(blockCoord.y, ChunkConstants.Size),
                    FloorDiv(blockCoord.z, ChunkConstants.Size));
                int localX = blockCoord.x - chunkCoord.x * ChunkConstants.Size;
                int localY = blockCoord.y - chunkCoord.y * ChunkConstants.Size;
                int localZ = blockCoord.z - chunkCoord.z * ChunkConstants.Size;
                int flatIndex = ChunkData.GetIndex(localX, localY, localZ);

                BlockEntity.BlockEntity entity =
                    _blockEntityTickScheduler.GetEntity(chunkCoord, flatIndex);

                if (entity != null)
                {
                    InventoryBehavior entityInv =
                        entity.GetBehavior<InventoryBehavior>();

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
                                Inventory.AddItem(slot.ItemId, slot.Count, maxStack);
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

                        Inventory.AddItem(drop.ItemId, drop.Count, maxStack);
                    }
                }
            }

            // Consume durability of held tool
            ItemStack heldItem = Inventory.GetSelectedItem();

            if (!heldItem.IsEmpty && heldItem.HasComponents)
            {
                // Modular tool: consume durability from ToolInstance
                ToolInstanceComponent toolComp = heldItem.Components.Get<ToolInstanceComponent>(
                    DataComponentTypes.ToolInstanceId);

                if (toolComp != null && !toolComp.Tool.IsBroken)
                {
                    ToolInstance tool = toolComp.Tool;
                    tool.SetCurrentDurability(tool.CurrentDurability - 1);

                    ItemStack updated = heldItem;
                    updated.Durability = tool.CurrentDurability;
                    DataComponentMap updatedMap = new();
                    updatedMap.Set(DataComponentTypes.ToolInstanceId,
                        new ToolInstanceComponent(tool));
                    updated.Components = updatedMap;
                    Inventory.SetSlot(Inventory.SelectedSlot, updated);
                }
            }

            // Delegate state mutation (set to air) to command processor
            if (_commandProcessor != null)
            {
                BreakBlockCommand cmd = new()
                {
                    Tick = 0, SequenceId = 0, PlayerId = 0, Position = blockCoord,
                };

                _commandProcessor.ProcessBreak(in cmd, _dirtiedChunks);
            }
            else
            {
                // Fallback: direct SetBlock if no processor wired
                _dirtiedChunks.Clear();
                _chunkManager.SetBlock(blockCoord, StateId.Air, _dirtiedChunks);
            }
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
                    int3 chunkCoord = new(
                        FloorDiv(hit.BlockCoord.x, ChunkConstants.Size),
                        FloorDiv(hit.BlockCoord.y, ChunkConstants.Size),
                        FloorDiv(hit.BlockCoord.z, ChunkConstants.Size));
                    int localX = hit.BlockCoord.x - chunkCoord.x * ChunkConstants.Size;
                    int localY = hit.BlockCoord.y - chunkCoord.y * ChunkConstants.Size;
                    int localZ = hit.BlockCoord.z - chunkCoord.z * ChunkConstants.Size;
                    int flatIndex = ChunkData.GetIndex(localX, localY, localZ);

                    BlockEntity.BlockEntity entity =
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
            ItemStack selectedItem = Inventory.GetSelectedItem();

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

            // Delegate validation (air check, player overlap) and state mutation to command processor
            if (_commandProcessor != null)
            {
                PlaceBlockCommand cmd = new()
                {
                    Tick = 0,
                    SequenceId = 0,
                    PlayerId = 0,
                    Position = placeCoord,
                    BlockState = placeState,
                    Face = NormalToBlockFace(hit.Normal),
                };

                CommandResult result = _commandProcessor.ProcessPlace(in cmd, _dirtiedChunks);

                if (result != CommandResult.Success)
                {
                    return;
                }
            }
            else
            {
                // Fallback: direct validation + SetBlock if no processor wired
                StateId existing = _chunkManager.GetBlock(placeCoord);

                if (existing != StateId.Air)
                {
                    bool isFluid = _nativeStateRegistry.States.IsCreated &&
                                   existing.Value < _nativeStateRegistry.States.Length &&
                                   _nativeStateRegistry.States[existing.Value].IsFluid;

                    if (!isFluid)
                    {
                        return;
                    }
                }

                if (_playerTransform != null)
                {
                    float3 feetPos = new(
                        _playerTransform.position.x,
                        _playerTransform.position.y,
                        _playerTransform.position.z);

                    Aabb playerBox = new(
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
            }

            // Play place sound after block is set
            if (_blockSoundPlayer != null)
            {
                _blockSoundPlayer.PlayBlockSound(placeState, SoundEventType.Place, placeCoord);
            }

            // Consume one item from inventory
            Inventory.RemoveFromSlot(Inventory.SelectedSlot, 1);

            _placeCooldown = _placeCooldownTime;
        }

        private StateId FindStateIdForBlock(ResourceId blockId)
        {
            IReadOnlyList<StateRegistryEntry> entries =
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

        private static BlockFace NormalToBlockFace(int3 normal)
        {
            if (normal.x > 0) { return BlockFace.East; }
            if (normal.x < 0) { return BlockFace.West; }
            if (normal.y > 0) { return BlockFace.Up; }
            if (normal.y < 0) { return BlockFace.Down; }
            if (normal.z > 0) { return BlockFace.North; }
            return BlockFace.South;
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        private void ResetMining()
        {
            IsMining = false;
            _miningProgress = 0f;
            _miningRequiredTime = 0f;
            _canHarvest = true;
        }
    }
}
