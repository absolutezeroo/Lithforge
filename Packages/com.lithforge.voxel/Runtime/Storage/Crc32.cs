namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     CRC-32 checksum implementation using the standard IEEE 802.3 polynomial (0xEDB88320).
    ///     Used by <see cref="ChunkSerializer"/> to verify chunk data integrity on save/load.
    /// </summary>
    internal static class Crc32
    {
        /// <summary>Pre-computed CRC lookup table (256 entries, built once in the static constructor).</summary>
        private static readonly uint[] s_table = new uint[256];

        /// <summary>Builds the CRC lookup table from the IEEE 802.3 polynomial.</summary>
        static Crc32()
        {
            const uint polynomial = 0xEDB88320u;

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }

                s_table[i] = crc;
            }
        }

        /// <summary>Computes the CRC-32 checksum over a byte range.</summary>
        public static uint Compute(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFFu;

            for (int i = offset; i < offset + length; i++)
            {
                byte index = (byte)((crc ^ data[i]) & 0xFF);
                crc = (crc >> 8) ^ s_table[index];
            }

            return crc ^ 0xFFFFFFFFu;
        }
    }
}
