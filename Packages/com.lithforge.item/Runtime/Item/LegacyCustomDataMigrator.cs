namespace Lithforge.Item
{
    /// <summary>
    /// Migrates legacy byte[] CustomData to typed DataComponentMap.
    /// Tries ToolInstance deserializer first, then ToolPartData.
    /// </summary>
    public static class LegacyCustomDataMigrator
    {
        /// <summary>
        /// Attempts to migrate raw CustomData bytes into a DataComponentMap
        /// by trying known serializers. Returns null if migration fails.
        /// </summary>
        public static DataComponentMap Migrate(byte[] customData)
        {
            if (customData == null || customData.Length == 0)
            {
                return null;
            }

            // Try ToolInstance first (most common case)
            if (ToolInstanceSerializer.TryDeserialize(customData, out ToolInstance tool))
            {
                DataComponentMap map = new();
                map.Set(DataComponentTypes.ToolInstanceId, new ToolInstanceComponent(tool));
                return map;
            }

            // Try ToolPartData
            if (ToolPartDataSerializer.TryDeserialize(customData, out ToolPartData partData))
            {
                DataComponentMap map = new();
                map.Set(DataComponentTypes.ToolPartDataId, new ToolPartDataComponent(partData));
                return map;
            }

            return null;
        }
    }
}
