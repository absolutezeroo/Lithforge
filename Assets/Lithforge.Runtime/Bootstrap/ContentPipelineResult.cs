using System.Collections.Generic;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;

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

        public ContentPipelineResult(
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            AtlasResult atlasResult,
            List<BiomeDefinition> biomeDefinitions,
            List<OreDefinition> oreDefinitions)
        {
            StateRegistry = stateRegistry;
            NativeStateRegistry = nativeStateRegistry;
            NativeAtlasLookup = nativeAtlasLookup;
            AtlasResult = atlasResult;
            BiomeDefinitions = biomeDefinitions;
            OreDefinitions = oreDefinitions;
        }
    }
}
