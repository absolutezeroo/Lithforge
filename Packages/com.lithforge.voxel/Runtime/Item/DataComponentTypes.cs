using System.IO;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Defines built-in component type IDs and registers them with
    /// <see cref="DataComponentRegistry"/>. Called once from ContentPipeline.
    /// </summary>
    public static class DataComponentTypes
    {
        public const int ToolInstanceId = 1;
        public const int ToolPartDataId = 2;

        public static void Initialize()
        {
            DataComponentRegistry.Register(new DataComponentType<ToolInstanceComponent>(
                ToolInstanceId,
                (BinaryWriter w, ToolInstanceComponent c) =>
                {
                    byte[] data = ToolInstanceSerializer.Serialize(c.Tool);
                    w.Write(data);
                },
                (BinaryReader r) =>
                {
                    // Read remaining bytes from this component's data segment
                    byte[] data = ReadRemainingBytes(r);
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(data);
                    return new ToolInstanceComponent(tool);
                }));

            DataComponentRegistry.Register(new DataComponentType<ToolPartDataComponent>(
                ToolPartDataId,
                (BinaryWriter w, ToolPartDataComponent c) =>
                {
                    byte[] data = ToolPartDataSerializer.Serialize(c.PartData);
                    w.Write(data);
                },
                (BinaryReader r) =>
                {
                    byte[] data = ReadRemainingBytes(r);
                    ToolPartData partData = ToolPartDataSerializer.Deserialize(data);
                    return new ToolPartDataComponent(partData);
                }));
        }

        private static byte[] ReadRemainingBytes(BinaryReader reader)
        {
            MemoryStream ms = reader.BaseStream as MemoryStream;

            if (ms != null)
            {
                int remaining = (int)(ms.Length - ms.Position);
                return reader.ReadBytes(remaining);
            }

            // Fallback: read into a list
            using (MemoryStream buffer = new())
            {
                reader.BaseStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }
    }
}
