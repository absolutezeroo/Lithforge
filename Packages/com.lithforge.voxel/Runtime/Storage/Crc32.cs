namespace Lithforge.Voxel.Storage
{
    internal static class Crc32
    {
        private static readonly uint[] s_table = new uint[256];

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
