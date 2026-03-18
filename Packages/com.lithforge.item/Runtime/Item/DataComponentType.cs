using System;
using System.IO;

namespace Lithforge.Item
{
    /// <summary>
    /// Registration descriptor binding a type ID to serialize/deserialize delegates
    /// for a specific <see cref="IDataComponent"/> implementation.
    /// </summary>
    public sealed class DataComponentType<T> where T : class, IDataComponent
    {
        public int Id { get; }

        public Action<BinaryWriter, T> Serializer { get; }

        public Func<BinaryReader, T> Deserializer { get; }

        public DataComponentType(int id, Action<BinaryWriter, T> serializer, Func<BinaryReader, T> deserializer)
        {
            Id = id;
            Serializer = serializer;
            Deserializer = deserializer;
        }
    }
}
