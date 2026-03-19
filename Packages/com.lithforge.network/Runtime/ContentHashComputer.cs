using System.Collections.Generic;
using System.Text;
using Lithforge.Voxel.Block;

namespace Lithforge.Network
{
    /// <summary>
    /// Computes a deterministic 128-bit content hash from the StateRegistry.
    /// Used during handshake to verify client and server have identical content.
    /// Uses FNV-1a 64-bit, computed in two halves (entries + states) for 128-bit output.
    /// </summary>
    public static class ContentHashComputer
    {
        /// <summary>
        /// FNV-1a 64-bit offset basis constant.
        /// </summary>
        private const ulong Fnv64OffsetBasis = 14695981039346656037UL;

        /// <summary>
        /// FNV-1a 64-bit prime multiplier constant.
        /// </summary>
        private const ulong Fnv64Prime = 1099511628211UL;

        /// <summary>
        /// Computes a ContentHash from the current state of the registry.
        /// Must be called after all blocks are registered and textures are patched,
        /// but before or after BakeNative().
        /// </summary>
        public static ContentHash Compute(StateRegistry registry)
        {
            // High half: hash over entry metadata (block identity + properties)
            ulong high = Fnv64OffsetBasis;
            IReadOnlyList<StateRegistryEntry> entries = registry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];
                high = HashString(high, entry.Id.ToString());
                high = HashInt(high, entry.StateCount);
                high = HashUShort(high, entry.BaseStateId);
                high = HashUShort(high, entry.BlockOrdinal);
                high = HashFloat(high, entry.Hardness);
                high = HashFloat(high, entry.BlastResistance);
                high = HashByte(high, entry.RequiresTool ? (byte)1 : (byte)0);
                high = HashByte(high, (byte)entry.MaterialType);
                high = HashInt(high, entry.RequiredToolLevel);
            }

            // Low half: hash over all BlockStateCompact data (per-state render/physics flags)
            ulong low = Fnv64OffsetBasis;
            int totalStates = registry.TotalStateCount;

            for (int i = 0; i < totalStates; i++)
            {
                BlockStateCompact state = registry.GetState(new StateId((ushort)i));
                low = HashUShort(low, state.BlockId);
                low = HashByte(low, state.Flags);
                low = HashByte(low, state.RenderLayer);
                low = HashByte(low, state.LightEmission);
                low = HashByte(low, state.LightFilter);
                low = HashByte(low, state.CollisionShape);
                low = HashUInt(low, state.MapColor);
                low = HashUShort(low, state.TexNorth);
                low = HashUShort(low, state.TexSouth);
                low = HashUShort(low, state.TexEast);
                low = HashUShort(low, state.TexWest);
                low = HashUShort(low, state.TexUp);
                low = HashUShort(low, state.TexDown);
            }

            return new ContentHash(high, low);
        }

        /// <summary>
        /// Folds a single byte into the running FNV-1a hash.
        /// </summary>
        private static ulong HashByte(ulong hash, byte value)
        {
            hash ^= value;
            hash *= Fnv64Prime;
            return hash;
        }

        /// <summary>
        /// Folds a ushort (2 bytes, little-endian order) into the running FNV-1a hash.
        /// </summary>
        private static ulong HashUShort(ulong hash, ushort value)
        {
            hash = HashByte(hash, (byte)(value & 0xFF));
            hash = HashByte(hash, (byte)(value >> 8));
            return hash;
        }

        /// <summary>
        /// Folds a 32-bit int (4 bytes, little-endian order) into the running FNV-1a hash.
        /// </summary>
        private static ulong HashInt(ulong hash, int value)
        {
            hash = HashByte(hash, (byte)(value & 0xFF));
            hash = HashByte(hash, (byte)((value >> 8) & 0xFF));
            hash = HashByte(hash, (byte)((value >> 16) & 0xFF));
            hash = HashByte(hash, (byte)((value >> 24) & 0xFF));
            return hash;
        }

        /// <summary>
        /// Folds a 32-bit uint (4 bytes, little-endian order) into the running FNV-1a hash.
        /// </summary>
        private static ulong HashUInt(ulong hash, uint value)
        {
            hash = HashByte(hash, (byte)(value & 0xFF));
            hash = HashByte(hash, (byte)((value >> 8) & 0xFF));
            hash = HashByte(hash, (byte)((value >> 16) & 0xFF));
            hash = HashByte(hash, (byte)((value >> 24) & 0xFF));
            return hash;
        }

        /// <summary>
        /// Folds a float (reinterpreted as 4 bytes via bit cast) into the running FNV-1a hash.
        /// </summary>
        private static ulong HashFloat(ulong hash, float value)
        {
            unsafe
            {
                uint bits = *(uint*)&value;
                return HashUInt(hash, bits);
            }
        }

        /// <summary>
        /// Folds a UTF-8 encoded string into the running FNV-1a hash, followed by its byte length as a terminator.
        /// </summary>
        private static ulong HashString(ulong hash, string value)
        {
            if (value == null)
            {
                return HashByte(hash, 0);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);

            for (int i = 0; i < bytes.Length; i++)
            {
                hash = HashByte(hash, bytes[i]);
            }

            // Hash length as terminator to avoid prefix collisions
            hash = HashInt(hash, bytes.Length);
            return hash;
        }
    }
}
