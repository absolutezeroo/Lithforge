using System;
using System.Collections.Generic;
using System.Globalization;

using Unity.Collections;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    ///     Managed state registry that assigns StateId ranges to blocks.
    ///     Accepts BlockRegistrationData with pre-computed state counts.
    ///     StateId(0) is always AIR — this is enforced at construction.
    ///     After all blocks are registered, call BakeNative() to produce the
    ///     Burst-compatible NativeStateRegistry.
    /// </summary>
    public sealed class StateRegistry
    {
        private readonly List<StateRegistryEntry> _entries = new();
        private readonly List<BlockStateCompact> _states = new();
        private bool _frozen;

        public StateRegistry()
        {
            // StateId(0) = AIR, hardcoded invariant
            BlockStateCompact airState = new()
            {
                BlockId = 0,
                Flags = BlockStateCompact.FlagAir,
                RenderLayer = 0,
                LightEmission = 0,
                LightFilter = 0,
                CollisionShape = 0,
                TextureIndexBase = 0,
                MapColor = 0x00000000,
            };
            _states.Add(airState);
        }

        public int TotalStateCount
        {
            get { return _states.Count; }
        }

        public IReadOnlyList<StateRegistryEntry> Entries
        {
            get { return _entries; }
        }

        /// <summary>
        ///     Registers a block via BlockRegistrationData and computes its state range.
        ///     Returns the base StateId assigned.
        /// </summary>
        public ushort Register(BlockRegistrationData data)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    $"Cannot register '{data.Id}' — state registry has been frozen.");
            }

            ushort baseId = (ushort)_states.Count;
            int stateCount = data.StateCount;

            if (baseId + stateCount > ushort.MaxValue)
            {
                throw new OverflowException(
                    $"StateId overflow registering '{data.Id}': " +
                    $"base={baseId}, count={stateCount}, max={ushort.MaxValue}.");
            }

            // BlockOrdinal is the sequential block index (0-based, excluding AIR)
            ushort blockOrdinal = (ushort)_entries.Count;

            byte flags = ComputeFlagsFromData(data);
            byte renderLayer = ParseRenderLayer(data.RenderLayer);
            byte collisionShape = ParseCollisionShape(data.CollisionShape);
            uint mapColor = ParseMapColor(data.MapColor);

            for (int i = 0; i < stateCount; i++)
            {
                BlockStateCompact state = new()
                {
                    BlockId = blockOrdinal,
                    Flags = flags,
                    RenderLayer = renderLayer,
                    LightEmission = (byte)data.LightEmission,
                    LightFilter = (byte)data.LightFilter,
                    CollisionShape = collisionShape,
                    TextureIndexBase = 0,
                    MapColor = mapColor,
                    TexNorth = 0,
                    TexSouth = 0,
                    TexEast = 0,
                    TexWest = 0,
                    TexUp = 0,
                    TexDown = 0,
                };
                _states.Add(state);
            }

            StateRegistryEntry entry = new(
                data.Id, baseId, stateCount, blockOrdinal, data.LootTable,
                data.Hardness, data.BlastResistance, data.RequiresTool,
                data.MaterialType, data.RequiredToolLevel, data.SoundGroup);
            _entries.Add(entry);

            return baseId;
        }

        /// <summary>
        ///     Freezes the registry and bakes to a NativeStateRegistry for Burst job access.
        ///     The caller is responsible for disposing the returned NativeStateRegistry.
        /// </summary>
        public NativeStateRegistry BakeNative(Allocator allocator)
        {
            _frozen = true;

            BlockStateCompact[] tempArray = _states.ToArray();
            NativeArray<BlockStateCompact> nativeStates = new(tempArray.Length, allocator, NativeArrayOptions.UninitializedMemory);
            nativeStates.CopyFrom(tempArray);

            return new NativeStateRegistry(nativeStates);
        }

        /// <summary>
        ///     Gets a managed BlockStateCompact by StateId value.
        /// </summary>
        public BlockStateCompact GetState(StateId id)
        {
            return _states[id.Value];
        }

        /// <summary>
        ///     Finds the StateRegistryEntry that owns the given StateId.
        ///     Returns null for StateId.Air or if no matching entry is found.
        ///     O(n) scan over entries — call infrequently (e.g., on block break), not per-frame.
        /// </summary>
        public StateRegistryEntry GetEntryForState(StateId id)
        {
            if (id.Value == 0)
            {
                return null;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                StateRegistryEntry entry = _entries[i];

                if (id.Value >= entry.BaseStateId && id.Value < entry.BaseStateId + entry.StateCount)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        ///     Patches the block entity type on a block's states, setting FlagHasBlockEntity
        ///     and storing the type ID on the entry. Must be called before BakeNative().
        /// </summary>
        public void PatchBlockEntityType(string blockIdString, string blockEntityTypeId)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "Cannot patch block entity type — state registry has been frozen.");
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                StateRegistryEntry entry = _entries[i];

                if (entry.Id.ToString() == blockIdString)
                {
                    entry.BlockEntityTypeId = blockEntityTypeId;

                    for (int offset = 0; offset < entry.StateCount; offset++)
                    {
                        int stateIndex = entry.BaseStateId + offset;
                        BlockStateCompact state = _states[stateIndex];
                        state.Flags |= BlockStateCompact.FlagHasBlockEntity;
                        _states[stateIndex] = state;
                    }

                    return;
                }
            }
        }

        /// <summary>
        ///     Patches the per-face texture indices on a state before BakeNative().
        ///     Must be called after Register() and before BakeNative().
        /// </summary>
        public void PatchTextures(
            StateId id,
            ushort texNorth, ushort texSouth,
            ushort texEast, ushort texWest,
            ushort texUp, ushort texDown)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "Cannot patch textures — state registry has been frozen.");
            }

            BlockStateCompact state = _states[id.Value];
            state.TexNorth = texNorth;
            state.TexSouth = texSouth;
            state.TexEast = texEast;
            state.TexWest = texWest;
            state.TexUp = texUp;
            state.TexDown = texDown;
            _states[id.Value] = state;
        }

        private static byte ComputeFlagsFromData(BlockRegistrationData data)
        {
            byte flags = 0;

            bool isOpaque = string.Equals(data.RenderLayer, "opaque", StringComparison.Ordinal);
            bool isFullCube = string.Equals(data.CollisionShape, "full_cube", StringComparison.Ordinal);
            bool emitsLight = data.LightEmission > 0;

            if (isOpaque)
            {
                flags |= BlockStateCompact.FlagOpaque;
            }

            if (isFullCube)
            {
                flags |= BlockStateCompact.FlagFullCube;
            }

            if (emitsLight)
            {
                flags |= BlockStateCompact.FlagEmitsLight;
            }

            if (data.IsFluid)
            {
                flags |= BlockStateCompact.FlagFluid;
            }

            if (data.HasBlockEntity)
            {
                flags |= BlockStateCompact.FlagHasBlockEntity;
            }

            return flags;
        }

        private static byte ParseRenderLayer(string renderLayer)
        {
            if (string.Equals(renderLayer, "cutout", StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(renderLayer, "translucent", StringComparison.Ordinal))
            {
                return 2;
            }

            return 0; // opaque
        }

        private static byte ParseCollisionShape(string collisionShape)
        {
            if (string.Equals(collisionShape, "none", StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(collisionShape, "full_cube", StringComparison.Ordinal))
            {
                return 1;
            }

            return 1; // default full_cube
        }

        /// <summary>
        ///     Parses a hex color string (#RRGGBB or #RRGGBBAA) to packed RGBA8 uint.
        ///     Returns default gray (0x808080FF) if the string is null or invalid.
        /// </summary>
        public static uint ParseMapColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return 0x808080FF;
            }

            string raw = hex;

            if (raw.Length > 0 && raw[0] == '#')
            {
                raw = raw.Substring(1);
            }

            if (raw.Length == 6)
            {

                if (uint.TryParse(raw, NumberStyles.HexNumber, null, out uint rgb))
                {
                    return rgb << 8 | 0xFF;
                }
            }
            else if (raw.Length == 8)
            {

                if (uint.TryParse(raw, NumberStyles.HexNumber, null, out uint rgba))
                {
                    return rgba;
                }
            }

            return 0x808080FF;
        }
    }
}
