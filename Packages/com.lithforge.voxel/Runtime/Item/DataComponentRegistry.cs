using System;
using System.Collections.Generic;
using System.IO;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    ///     Static registry mapping int IDs to component types and their serializers.
    ///     Initialized once during ContentPipeline.Build().
    /// </summary>
    public static class DataComponentRegistry
    {
        private static readonly Dictionary<int, ComponentEntry> s_entries = new();

        private static readonly Dictionary<Type, int> s_typeToId = new();

        /// <summary>
        ///     Registers a component type with its serializer/deserializer.
        /// </summary>
        public static void Register<T>(DataComponentType<T> componentType) where T : class, IDataComponent
        {
            ComponentEntry entry = new(
                componentType.Id,
                typeof(T),
                (w, c) => componentType.Serializer(w, (T)c),
                r => componentType.Deserializer(r));

            s_entries[componentType.Id] = entry;
            s_typeToId[typeof(T)] = componentType.Id;
        }

        /// <summary>
        ///     Writes all components in the map with type tags.
        ///     Format: [byte: count] per component: [ushort: typeId] [int: dataLen] [byte[]: data]
        /// </summary>
        public static void Serialize(DataComponentMap map, BinaryWriter writer)
        {
            if (map == null || map.IsEmpty)
            {
                writer.Write((byte)0);
                return;
            }

            writer.Write((byte)map.Count);

            foreach (KeyValuePair<int, IDataComponent> kvp in map)
            {
                int typeId = kvp.Key;
                IDataComponent component = kvp.Value;

                writer.Write((ushort)typeId);

                if (s_entries.TryGetValue(typeId, out ComponentEntry entry))
                {
                    // Write data to temp buffer to get length
                    using (MemoryStream tempMs = new())
                    {
                        using (BinaryWriter tempW = new(tempMs))
                        {
                            entry.SerializeFunc(tempW, component);
                        }

                        byte[] data = tempMs.ToArray();
                        writer.Write(data.Length);
                        writer.Write(data);
                    }
                }
                else
                {
                    // Unknown type — write zero-length data
                    writer.Write(0);
                }
            }
        }

        /// <summary>
        ///     Reads tagged components from a binary stream.
        /// </summary>
        public static DataComponentMap Deserialize(BinaryReader reader)
        {
            int count = reader.ReadByte();

            if (count == 0)
            {
                return null;
            }

            DataComponentMap map = new();

            for (int i = 0; i < count; i++)
            {
                int typeId = reader.ReadUInt16();
                int dataLen = reader.ReadInt32();

                if (s_entries.TryGetValue(typeId, out ComponentEntry entry))
                {
                    byte[] data = reader.ReadBytes(dataLen);

                    using (MemoryStream ms = new(data))
                    using (BinaryReader dataReader = new(ms))
                    {
                        IDataComponent component = entry.DeserializeFunc(dataReader);
                        map.Set(typeId, component);
                    }
                }
                else
                {
                    // Skip unknown component data
                    reader.ReadBytes(dataLen);
                }
            }

            return map.IsEmpty ? null : map;
        }

        /// <summary>
        ///     Looks up the type ID for a component instance.
        ///     Returns -1 if the type is not registered.
        /// </summary>
        public static int GetTypeId(IDataComponent component)
        {
            if (component == null)
            {
                return -1;
            }

            Type type = component.GetType();

            if (s_typeToId.TryGetValue(type, out int id))
            {
                return id;
            }

            return -1;
        }

        /// <summary>
        ///     Serializes a single component to a BinaryWriter.
        /// </summary>
        public static void SerializeComponent(IDataComponent component, BinaryWriter writer)
        {
            int typeId = GetTypeId(component);

            if (typeId >= 0 && s_entries.TryGetValue(typeId, out ComponentEntry entry))
            {
                entry.SerializeFunc(writer, component);
            }
        }

        /// <summary>
        ///     Deserializes a single component from a BinaryReader given its type ID.
        /// </summary>
        public static IDataComponent DeserializeComponent(int typeId, BinaryReader reader)
        {
            if (s_entries.TryGetValue(typeId, out ComponentEntry entry))
            {
                return entry.DeserializeFunc(reader);
            }

            return null;
        }

        private sealed class ComponentEntry
        {
            public readonly Func<BinaryReader, IDataComponent> DeserializeFunc;
            public readonly Action<BinaryWriter, IDataComponent> SerializeFunc;
            public Type ComponentType;
            public int Id;

            public ComponentEntry(
                int id,
                Type componentType,
                Action<BinaryWriter, IDataComponent> serializeFunc,
                Func<BinaryReader, IDataComponent> deserializeFunc)
            {
                Id = id;
                ComponentType = componentType;
                SerializeFunc = serializeFunc;
                DeserializeFunc = deserializeFunc;
            }
        }
    }
}
