using System.IO;

using Lithforge.Core.Data;
using Lithforge.Item;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    ///     Serializes/deserializes ToolPartData to/from byte[] for component storage.
    ///     Format: [byte version=1] [byte partType] [string materialId]
    /// </summary>
    public static class ToolPartDataSerializer
    {
        private const byte Version = 1;

        public static byte[] Serialize(ToolPartData data)
        {
            using (MemoryStream ms = new())
            {
                using (BinaryWriter w = new(ms))
                {
                    w.Write(Version);
                    w.Write((byte)data.PartType);
                    w.Write(data.MaterialId.ToString());
                }

                return ms.ToArray();
            }
        }

        public static ToolPartData Deserialize(byte[] bytes)
        {
            using (MemoryStream ms = new(bytes))
            {
                using (BinaryReader r = new(ms))
                {
                    byte version = r.ReadByte();

                    if (version != Version)
                    {
                        throw new InvalidDataException(
                            "Unsupported ToolPartData version " + version);
                    }

                    ToolPartType partType = (ToolPartType)r.ReadByte();
                    ResourceId materialId = ResourceId.Parse(r.ReadString());

                    return new ToolPartData
                    {
                        PartType = partType, MaterialId = materialId,
                    };
                }
            }
        }

        public static bool TryDeserialize(byte[] bytes, out ToolPartData data)
        {
            data = default;

            if (bytes == null || bytes.Length < 3)
            {
                return false;
            }

            try
            {
                data = Deserialize(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
