using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Content;
using Unity.Collections;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    /// Orchestrates the full content loading pipeline:
    ///   Phase 1: Load block definitions
    ///   Phase 2: Register blocks in StateRegistry
    ///   Phase 3: Load blockstate definitions
    ///   Phase 4: Load and resolve block models
    ///   Phase 5: Resolve blockstate variants to per-face textures
    ///   Phase 6: Build texture atlas
    ///   Phase 7: Patch texture indices into StateRegistry
    ///   Phase 8: Load biome and ore definitions
    ///   Phase 9: BakeNative (freeze) + build NativeAtlasLookup
    /// </summary>
    public sealed class ContentPipeline
    {
        private readonly ILogger _logger;
        private readonly ContentValidator _validator;

        public ContentPipeline(ILogger logger, ContentValidator validator)
        {
            _logger = logger;
            _validator = validator;
        }

        public ContentPipelineResult Build(string contentRoot)
        {
            // Phase 1: Load block definitions
            BlockDefinitionLoader blockLoader = new BlockDefinitionLoader(_logger, _validator);
            List<BlockDefinition> definitions = blockLoader.LoadAll(contentRoot);

            // Phase 2: Register blocks in StateRegistry
            StateRegistry stateRegistry = new StateRegistry();

            for (int i = 0; i < definitions.Count; i++)
            {
                stateRegistry.Register(definitions[i]);
            }

            _logger.LogInfo(
                $"Registered {definitions.Count} blocks, {stateRegistry.TotalStateCount} states.");

            // Phase 3: Load blockstate definitions
            BlockstateLoader blockstateLoader = new BlockstateLoader(_logger);
            Dictionary<ResourceId, BlockstateDefinition> blockstates =
                blockstateLoader.LoadAll(contentRoot);

            // Phase 4: Load and resolve block models
            BlockModelLoader modelLoader = new BlockModelLoader(_logger);
            Dictionary<ResourceId, BlockModel> rawModels = modelLoader.LoadAll(contentRoot);

            BlockModelResolver modelResolver = new BlockModelResolver(_logger);
            Dictionary<ResourceId, ResolvedFaceTextures> resolvedModels =
                modelResolver.ResolveAll(rawModels);

            // Phase 5: Resolve blockstate variants to per-face textures
            BlockstateResolver blockstateResolver = new BlockstateResolver(_logger);
            Dictionary<StateId, ResolvedFaceTextures> resolvedFaces =
                blockstateResolver.ResolveAll(stateRegistry.Entries, blockstates, resolvedModels);

            // Phase 6: Build texture atlas
            AtlasBuilder atlasBuilder = new AtlasBuilder(_logger);
            AtlasResult atlasResult = atlasBuilder.Build(resolvedFaces, contentRoot);

            // Phase 7: Patch texture indices into StateRegistry
            foreach (KeyValuePair<StateId, ResolvedFaceTextures> kvp in resolvedFaces)
            {
                StateId id = kvp.Key;
                ResolvedFaceTextures faces = kvp.Value;

                ushort texNorth = GetTextureIndex(atlasResult, faces.North);
                ushort texSouth = GetTextureIndex(atlasResult, faces.South);
                ushort texEast = GetTextureIndex(atlasResult, faces.East);
                ushort texWest = GetTextureIndex(atlasResult, faces.West);
                ushort texUp = GetTextureIndex(atlasResult, faces.Up);
                ushort texDown = GetTextureIndex(atlasResult, faces.Down);

                stateRegistry.PatchTextures(id, texNorth, texSouth, texEast, texWest, texUp, texDown);
            }

            // Phase 8: Load biome and ore definitions
            BiomeDefinitionLoader biomeLoader = new BiomeDefinitionLoader(_logger);
            List<BiomeDefinition> biomeDefinitions = biomeLoader.LoadAll(contentRoot);
            _logger.LogInfo($"Loaded {biomeDefinitions.Count} biome definitions.");

            OreDefinitionLoader oreLoader = new OreDefinitionLoader(_logger);
            List<OreDefinition> oreDefinitions = oreLoader.LoadAll(contentRoot);
            _logger.LogInfo($"Loaded {oreDefinitions.Count} ore definitions.");

            // Phase 9: BakeNative + build NativeAtlasLookup
            NativeStateRegistry nativeStateRegistry = stateRegistry.BakeNative(Allocator.Persistent);

            NativeAtlasLookup nativeAtlasLookup = BakeAtlasLookup(stateRegistry, atlasResult);

            return new ContentPipelineResult(
                stateRegistry,
                nativeStateRegistry,
                nativeAtlasLookup,
                atlasResult,
                biomeDefinitions,
                oreDefinitions);
        }

        private static ushort GetTextureIndex(AtlasResult atlas, ResourceId textureId)
        {
            if (atlas.IndexByTexture.TryGetValue(textureId, out int index))
            {
                return (ushort)index;
            }

            return (ushort)atlas.MissingTextureIndex;
        }

        private static NativeAtlasLookup BakeAtlasLookup(
            StateRegistry stateRegistry,
            AtlasResult atlasResult)
        {
            int totalStates = stateRegistry.TotalStateCount;
            NativeArray<AtlasEntry> entries = new NativeArray<AtlasEntry>(
                totalStates, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < totalStates; i++)
            {
                BlockStateCompact state = stateRegistry.GetState(new StateId((ushort)i));
                entries[i] = new AtlasEntry
                {
                    TexPosX = state.TexEast,
                    TexNegX = state.TexWest,
                    TexPosY = state.TexUp,
                    TexNegY = state.TexDown,
                    TexPosZ = state.TexSouth,
                    TexNegZ = state.TexNorth,
                };
            }

            int textureCount = 0;

            if (atlasResult.TextureArray != null)
            {
                textureCount = atlasResult.TextureArray.depth;
            }

            return new NativeAtlasLookup(entries, textureCount);
        }
    }
}
