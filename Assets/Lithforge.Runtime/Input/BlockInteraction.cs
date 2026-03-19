using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Item.Loot;
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
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
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
        /// <summary>Sort comparison for ordering tool traits by priority during mining calculation.</summary>
        private static readonly Comparison<IToolTrait> s_traitPrioritySort =
            (a, b) => a.Priority.CompareTo(b.Priority);

        /// <summary>Maps ToolType to its corresponding mineable tag ResourceId for correct-tool checks.</summary>
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

        /// <summary>Array of digit key codes (1-9) for hotbar slot selection.</summary>
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

        /// <summary>Reusable list for collecting dirtied chunk coordinates after block changes.</summary>
        private readonly List<int3> _dirtiedChunks = new();

        /// <summary>Scheduler for ticking block entities (furnaces, chests, etc.).</summary>
        private BlockEntityTickScheduler _blockEntityTickScheduler;

        /// <summary>Wireframe highlight shown around the targeted block.</summary>
        private BlockHighlight _blockHighlight;

        /// <summary>Audio player for block dig, place, and break sounds.</summary>
        private BlockSoundPlayer _blockSoundPlayer;

        /// <summary>Whether the current tool can harvest drops from the mined block.</summary>
        private bool _canHarvest;

        /// <summary>Chunk manager for reading block state and applying changes.</summary>
        private ChunkManager _chunkManager;

        /// <summary>Command processor that validates and executes block place and break commands.</summary>
        private ICommandProcessor _commandProcessor;

        /// <summary>Default maximum stack size for items without an explicit definition.</summary>
        private int _defaultMaxStackSize;

        /// <summary>Mining speed multiplier when using bare hands (no tool).</summary>
        private float _handMiningMultiplier;

        /// <summary>Maximum reach distance for block interaction raycasts.</summary>
        private float _interactionRange;

        /// <summary>Cached delegate for solid block checks used by VoxelRaycast.</summary>
        private Func<int3, bool> _isSolidDelegate;

        /// <summary>Item registry for looking up item definitions and max stack sizes.</summary>
        private ItemRegistry _itemRegistry;

        /// <summary>Random number generator for loot table resolution.</summary>
        private Random _lootRandom;

        /// <summary>Resolver that evaluates loot tables to produce drop lists.</summary>
        private LootResolver _lootResolver;

        /// <summary>Minimum time required to break any block, even with instant tools.</summary>
        private float _minBreakTime;

        /// <summary>World coordinate of the block currently being mined.</summary>
        private int3 _miningBlockCoord;

        /// <summary>Number of ticks between mining hit sound effects.</summary>
        private int _miningHitInterval = 4;

        /// <summary>Counter tracking ticks since last mining hit sound.</summary>
        private int _miningHitTickCounter;

        /// <summary>Accumulated mining progress in seconds toward breaking the target block.</summary>
        private float _miningProgress;

        /// <summary>Total time required to break the current target block.</summary>
        private float _miningRequiredTime;

        /// <summary>Native state registry for Burst-compatible block property lookups.</summary>
        private NativeStateRegistry _nativeStateRegistry;

        /// <summary>Network callback invoked when mining begins, for server-side break speed validation.</summary>
        private Action<int3> _onStartDigging;

        /// <summary>Remaining cooldown time before the next block placement is allowed.</summary>
        private float _placeCooldown;

        /// <summary>Cooldown duration between successive block placements.</summary>
        private float _placeCooldownTime;

        /// <summary>Player controller for fly mode and scroll-wheel gating.</summary>
        private PlayerController _playerController;

        /// <summary>Half-width of the player AABB for placement overlap checks.</summary>
        private float _playerHalfWidth;

        /// <summary>Height of the player AABB for placement overlap checks.</summary>
        private float _playerHeight;

        /// <summary>Player transform providing feet position for raycasting and overlap checks.</summary>
        private Transform _playerTransform;

        /// <summary>Manager for block entity container screens dispatched on right-click.</summary>
        private ContainerScreenManager _screenManager;

        /// <summary>State registry for looking up block definitions by state ID.</summary>
        private StateRegistry _stateRegistry;

        /// <summary>Tag registry for checking mineable tool tag membership.</summary>
        private TagRegistry _tagRegistry;

        /// <summary>Mining speed multiplier applied when using the correct tool type.</summary>
        private float _toolMiningMultiplier;

        /// <summary>Registry of tool traits for modifier-based mining speed adjustments.</summary>
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

        /// <summary>Per-frame update handling hotbar selection, raycasting, mining, and placement.</summary>
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
        /// <summary>Replaces the command processor used for block place and break validation.</summary>
        public void SetCommandProcessor(ICommandProcessor processor)
        {
            _commandProcessor = processor;
        }

        /// <summary>
        ///     Sets a callback invoked when the player begins mining a new block.
        ///     In network mode, this sends a StartDiggingCmd message to the server.
        /// </summary>
        /// <summary>Sets the network callback invoked when the player starts digging a block.</summary>
        public void SetStartDiggingCallback(Action<int3> callback)
        {
            _onStartDigging = callback;
        }

        /// <summary>Initializes the interaction system with world state, physics, and inventory references.</summary>
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
        /// <summary>Wires block entity references for container screen dispatch and tick scheduling.</summary>
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
        /// <summary>Configures the block sound player and mining hit sound interval.</summary>
        public void SetBlockSoundPlayer(BlockSoundPlayer player, int miningHitInterval)
        {
            _blockSoundPlayer = player;
            _miningHitInterval = miningHitInterval;
        }

        /// <summary>Checks digit key presses (1-9) and updates the hotbar selected slot.</summary>
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

        /// <summary>Handles scroll wheel for hotbar cycling or fly speed adjustment.</summary>
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

        /// <summary>Handles left-click mining initiation, cancellation, and target changes.</summary>
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

        /// <summary>Begins mining a block, calculating break time from tool speed, block hardness, and traits.</summary>
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

                if (heldItem is
                    {
                        IsEmpty: false,
                        HasComponents: true,
                    })
                {
                    ToolInstanceComponent toolComp = heldItem.Components.Get<ToolInstanceComponent>(
                        DataComponentTypes.ToolInstanceId);

                    if (toolComp != null)
                    {
                        tool = toolComp.Tool;
                    }
                }

                // Broken tools act as bare hand
                if (tool is
                    {
                        IsBroken: true,
                    })
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

        /// <summary>Completes a block break, resolving loot drops, applying durability, and cleaning up block entities.</summary>
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
                                int maxStack = itemDef?.MaxStackSize ?? _defaultMaxStackSize;
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
                        int maxStack = itemDef?.MaxStackSize ?? _defaultMaxStackSize;

                        Inventory.AddItem(drop.ItemId, drop.Count, maxStack);
                    }
                }
            }

            // Consume durability of held tool
            ItemStack heldItem = Inventory.GetSelectedItem();

            if (heldItem is
                {
                    IsEmpty: false,
                    HasComponents: true,
                })
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

        /// <summary>Handles right-click block placement, block entity interaction, and repair kit usage.</summary>
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
