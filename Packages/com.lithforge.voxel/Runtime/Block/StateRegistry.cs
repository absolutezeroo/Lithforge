using System;
using System.Collections.Generic;
using Lithforge.Core.Data;
using Unity.Collections;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Managed state registry that assigns StateId ranges to BlockDefinitions.
    /// Computes the cartesian product of properties for each block.
    /// StateId(0) is always AIR — this is enforced at construction.
    ///
    /// After all blocks are registered, call BakeNative() to produce the
    /// Burst-compatible NativeStateRegistry.
    /// </summary>
    public sealed class StateRegistry
    {
        private readonly List<StateRegistryEntry> _entries = new List<StateRegistryEntry>();
        private readonly List<BlockStateCompact> _states = new List<BlockStateCompact>();
        private bool _frozen;

        public int TotalStateCount
        {
            get { return _states.Count; }
        }

        public IReadOnlyList<StateRegistryEntry> Entries
        {
            get { return _entries; }
        }

        public StateRegistry()
        {
            // StateId(0) = AIR, hardcoded invariant
            BlockStateCompact airState = new BlockStateCompact
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

        /// <summary>
        /// Registers a block definition and computes its state range.
        /// Returns the base StateId assigned.
        /// </summary>
        public ushort Register(BlockDefinition definition)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    $"Cannot register '{definition.Id}' — state registry has been frozen.");
            }

            ushort baseId = (ushort)_states.Count;
            int stateCount = definition.ComputeStateCount();

            if (baseId + stateCount > ushort.MaxValue)
            {
                throw new OverflowException(
                    $"StateId overflow registering '{definition.Id}': " +
                    $"base={baseId}, count={stateCount}, max={ushort.MaxValue}.");
            }

            // BlockOrdinal is the sequential block index (0-based, excluding AIR)
            ushort blockOrdinal = (ushort)_entries.Count;

            byte flags = ComputeFlags(definition);
            byte renderLayer = ParseRenderLayer(definition.RenderLayer);
            byte collisionShape = ParseCollisionShape(definition.CollisionShape);
            uint mapColor = ParseMapColor(definition.MapColor);

            for (int i = 0; i < stateCount; i++)
            {
                BlockStateCompact state = new BlockStateCompact
                {
                    BlockId = blockOrdinal,
                    Flags = flags,
                    RenderLayer = renderLayer,
                    LightEmission = (byte)definition.LightEmission,
                    LightFilter = (byte)definition.LightFilter,
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

            StateRegistryEntry entry = new StateRegistryEntry(definition, baseId, stateCount, blockOrdinal);
            _entries.Add(entry);

            return baseId;
        }

        /// <summary>
        /// Freezes the registry and bakes to a NativeStateRegistry for Burst job access.
        /// The caller is responsible for disposing the returned NativeStateRegistry.
        /// </summary>
        public NativeStateRegistry BakeNative(Allocator allocator)
        {
            _frozen = true;

            BlockStateCompact[] tempArray = _states.ToArray();
            NativeArray<BlockStateCompact> nativeStates =
                new NativeArray<BlockStateCompact>(tempArray.Length, allocator, NativeArrayOptions.UninitializedMemory);
            nativeStates.CopyFrom(tempArray);

            return new NativeStateRegistry(nativeStates);
        }

        /// <summary>
        /// Gets a managed BlockStateCompact by StateId value.
        /// </summary>
        public BlockStateCompact GetState(StateId id)
        {
            return _states[id.Value];
        }

        /// <summary>
        /// Patches the per-face texture indices on a state before BakeNative().
        /// Must be called after Register() and before BakeNative().
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

        private static byte ComputeFlags(BlockDefinition definition)
        {
            byte flags = 0;

            bool isOpaque = string.Equals(definition.RenderLayer, "opaque", StringComparison.Ordinal);
            bool isFullCube = string.Equals(definition.CollisionShape, "full_cube", StringComparison.Ordinal);
            bool emitsLight = definition.LightEmission > 0;

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
        /// Parses a hex color string (#RRGGBB or #RRGGBBAA) to packed RGBA8 uint.
        /// Returns default gray (0x808080FF) if the string is null or invalid.
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
                uint rgb;

                if (uint.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out rgb))
                {
                    return (rgb << 8) | 0xFF;
                }
            }
            else if (raw.Length == 8)
            {
                uint rgba;

                if (uint.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out rgba))
                {
                    return rgba;
                }
            }

            return 0x808080FF;
        }
    }
}
