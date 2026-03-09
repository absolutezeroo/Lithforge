using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
using Lithforge.Voxel.Tag;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    /// Result of the content pipeline build.
    /// Contains all registries and atlas data needed for runtime operation.
    /// </summary>
    public sealed class ContentPipelineResult
    {
        public StateRegistry StateRegistry { get; }
        public NativeStateRegistry NativeStateRegistry { get; }
        public NativeAtlasLookup NativeAtlasLookup { get; }
        public AtlasResult AtlasResult { get; }
        public List<BiomeDefinition> BiomeDefinitions { get; }
        public List<OreDefinition> OreDefinitions { get; }
        public List<ItemDefinition> ItemDefinitions { get; }
        public Dictionary<ResourceId, LootTableDefinition> LootTables { get; }
        public TagRegistry TagRegistry { get; }

        public ContentPipelineResult(
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            AtlasResult atlasResult,
            List<BiomeDefinition> biomeDefinitions,
            List<OreDefinition> oreDefinitions,
            List<ItemDefinition> itemDefinitions,
            Dictionary<ResourceId, LootTableDefinition> lootTables,
            TagRegistry tagRegistry)
        {
            StateRegistry = stateRegistry;
            NativeStateRegistry = nativeStateRegistry;
            NativeAtlasLookup = nativeAtlasLookup;
            AtlasResult = atlasResult;
            BiomeDefinitions = biomeDefinitions;
            OreDefinitions = oreDefinitions;
            ItemDefinitions = itemDefinitions;
            LootTables = lootTables;
            TagRegistry = tagRegistry;
        }
    }
}
