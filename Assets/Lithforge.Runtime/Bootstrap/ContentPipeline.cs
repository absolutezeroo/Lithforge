using System;
using System.Collections.Generic;
using System.Text;

using Lithforge.Core.Data;
using Lithforge.Core.Validation;
using Lithforge.Item;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Bootstrap.Phases;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Content.Mods;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Orchestrates the full content loading pipeline using ScriptableObjects.
    ///     Phases 1-6.5 (blocks, models, textures, atlas) run inline as tightly-coupled code.
    ///     Phases 7+ are modular: each implements <see cref="IContentPhase" /> and reads/writes
    ///     a shared <see cref="ContentPhaseContext" />.
    /// </summary>
    public sealed class ContentPipeline
    {
        /// <summary>Tile size in pixels for atlas texture entries.</summary>
        private readonly int _atlasTileSize;

        /// <summary>Logger for pipeline diagnostics.</summary>
        private readonly ILogger _logger;

        /// <summary>Content validator for checking definition integrity.</summary>
        private readonly ContentValidator _validator;

        /// <summary>Creates a content pipeline with the given logger, validator, and atlas tile size.</summary>
        public ContentPipeline(ILogger logger, ContentValidator validator, int atlasTileSize = 16)
        {
            _logger = logger;
            _validator = validator;
            _atlasTileSize = atlasTileSize;
        }

        /// <summary>
        ///     The result of the content pipeline, available after Build() iteration completes.
        /// </summary>
        public ContentPipelineResult Result { get; private set; }

        /// <summary>
        ///     Builds the content pipeline as an iterator, yielding a phase description string
        ///     between each processing phase. The final result is stored in the Result property.
        ///     Can be consumed from a coroutine to allow frame yields between phases.
        /// </summary>
        public IEnumerable<string> Build()
        {
            ContentPhaseContext ctx = new()
            {
                Logger = _logger, Validator = _validator, AtlasTileSize = _atlasTileSize,
            };

            // Phase 1: Load block definitions
            yield return "Loading blocks...";
            BlockDefinition[] blocks = Resources.LoadAll<BlockDefinition>("Content/Blocks");
            ctx.BlockDefinitions = blocks;
            _logger.LogInfo($"Loaded {blocks.Length} block definitions.");

            // Phase 2: Register blocks in StateRegistry
            yield return "Registering states...";
            StateRegistry stateRegistry = new();
            Dictionary<string, BlockDefinition> blockLookup = new();

            for (int i = 0; i < blocks.Length; i++)
            {
                BlockDefinition block = blocks[i];
                ResourceId id = new(block.Namespace, block.BlockName);

                string lootTableStr = null;

                if (block.LootTable != null)
                {
                    lootTableStr = block.LootTable.Namespace + ":" + block.LootTable.TableName;
                }

                BlockRegistrationData regData = new(
                    id,
                    block.ComputeStateCount(),
                    block.RenderLayerString,
                    block.CollisionShapeString,
                    block.LightEmission,
                    block.LightFilter,
                    block.MapColor,
                    lootTableStr,
                    (float)block.Hardness,
                    (float)block.BlastResistance,
                    block.RequiresTool,
                    block.IsFluid,
                    block.MaterialType,
                    block.RequiredToolLevel,
                    soundGroup: block.SoundGroup);

                stateRegistry.Register(regData);
                blockLookup[id.ToString()] = block;
            }

            ctx.StateRegistry = stateRegistry;
            ctx.BlockLookup = blockLookup;
            _logger.LogInfo(
                $"Registered {blocks.Length} blocks, {stateRegistry.TotalStateCount} states.");

            // Phase 2.5: Load block entity definitions and patch StateRegistry
            yield return "Loading block entities...";
            BlockEntityDefinition[] blockEntityDefs =
                Resources.LoadAll<BlockEntityDefinition>("Content/BlockEntities");

            for (int i = 0; i < blockEntityDefs.Length; i++)
            {
                BlockEntityDefinition beDef = blockEntityDefs[i];
                stateRegistry.PatchBlockEntityType(beDef.BlockIdString, beDef.BlockEntityTypeId);
            }

            _logger.LogInfo($"Patched {blockEntityDefs.Length} block entity definitions.");

            // Phase 3: Resolve block models via ContentModelResolver
            yield return "Resolving models...";
            ContentModelResolver modelResolver = new();
            ctx.ModelResolver = modelResolver;
            Dictionary<BlockModel, ResolvedFaceTextures2D> resolvedModelCache = new();

            // Phase 4: Resolve blockstate variants to per-face textures
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces = new();

            IReadOnlyList<StateRegistryEntry> entries = stateRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (!blockLookup.TryGetValue(entry.Id.ToString(), out BlockDefinition block))
                {
                    continue;
                }

                BlockStateMapping mapping = block.BlockStateMapping;

                if (mapping == null)
                {
                    _logger.LogWarning($"No BlockStateMapping for '{entry.Id}'.");
                    continue;
                }

                for (int offset = 0; offset < entry.StateCount; offset++)
                {
                    StateId stateId = new((ushort)(entry.BaseStateId + offset));
                    string variantKey = BuildVariantKey(block, offset);

                    BlockStateVariantEntry variant = FindVariant(mapping, variantKey);

                    if (variant == null)
                    {
                        _logger.LogWarning(
                            $"BlockState '{entry.Id}' has no variant for key '{variantKey}'.");
                        continue;
                    }

                    if (variant.Model == null)
                    {
                        _logger.LogWarning(
                            $"Variant '{variantKey}' of '{entry.Id}' has no model assigned.");
                        continue;
                    }

                    if (!resolvedModelCache.TryGetValue(variant.Model, out ResolvedFaceTextures2D faces))
                    {
                        faces = modelResolver.Resolve(variant.Model);
                        resolvedModelCache[variant.Model] = faces;
                    }

                    resolvedFaces[stateId] = faces;
                }
            }

            _logger.LogInfo($"Resolved {resolvedFaces.Count} block state face textures.");

            // Phase 5: Resolve per-face tint types and overlay textures from model elements.
            // Must run BEFORE atlas building so overlay textures are included in the atlas.
            yield return "Resolving overlays and tints...";
            ResolveOverlaysAndTints(modelResolver, stateRegistry, blockLookup, resolvedFaces);

            // Phase 6: Build texture atlas (includes both base and overlay textures)
            yield return "Building texture atlas...";
            AtlasBuilder atlasBuilder = new(_logger, _atlasTileSize);
            AtlasResult atlasResult = atlasBuilder.Build(resolvedFaces);
            ctx.AtlasResult = atlasResult;

            // Phase 6.5: Patch texture indices into StateRegistry
            yield return "Patching texture indices...";
            foreach (KeyValuePair<StateId, ResolvedFaceTextures2D> kvp in resolvedFaces)
            {
                StateId id = kvp.Key;
                ResolvedFaceTextures2D faces = kvp.Value;

                ushort texNorth = GetTextureIndex(atlasResult, faces.North);
                ushort texSouth = GetTextureIndex(atlasResult, faces.South);
                ushort texEast = GetTextureIndex(atlasResult, faces.East);
                ushort texWest = GetTextureIndex(atlasResult, faces.West);
                ushort texUp = GetTextureIndex(atlasResult, faces.Up);
                ushort texDown = GetTextureIndex(atlasResult, faces.Down);

                stateRegistry.PatchTextures(id, texNorth, texSouth, texEast, texWest, texUp, texDown);
            }

            ctx.ResolvedFaces = resolvedFaces;

            // Initialize data component type registry (before item loading, so deserialization works)
            DataComponentTypes.Initialize();

            IContentPhase[] preModPhases =
            {
                new LoadBiomesAndOresPhase(),
                new LoadItemsPhase(),
                new LoadToolMaterialsPhase(),
                new LoadToolDefinitionsPhase(),
                new LoadToolTraitsPhase(),
                new LoadPartBuilderRecipesPhase(),
                new LoadLootTablesPhase(),
                new LoadTagsPhase(),
                new LoadCraftingRecipesPhase(),
                new BuildItemRegistryPhase(),
            };

            for (int i = 0; i < preModPhases.Length; i++)
            {
                yield return preModPhases[i].Description;
                preModPhases[i].Execute(ctx);
            }

            yield return "Loading mods...";
            ModLoader modLoader = new(_logger);
            modLoader.LoadAllMods();

            for (int i = 0; i < modLoader.LoadedBlocks.Count; i++)
            {
                BlockDefinition modBlock = modLoader.LoadedBlocks[i];
                ResourceId modId = new(modBlock.Namespace, modBlock.BlockName);

                string modLootStr = null;

                if (modBlock.LootTable != null)
                {
                    modLootStr = modBlock.LootTable.Namespace + ":" + modBlock.LootTable.TableName;
                }

                BlockRegistrationData modRegData = new(
                    modId,
                    modBlock.ComputeStateCount(),
                    modBlock.RenderLayerString,
                    modBlock.CollisionShapeString,
                    modBlock.LightEmission,
                    modBlock.LightFilter,
                    modBlock.MapColor,
                    modLootStr,
                    (float)modBlock.Hardness,
                    (float)modBlock.BlastResistance,
                    modBlock.RequiresTool,
                    modBlock.IsFluid,
                    modBlock.MaterialType,
                    modBlock.RequiredToolLevel);

                stateRegistry.Register(modRegData);
            }

            // TintOverlay.overlayTexIndex uses 10 bits (max index 1023), which is the binding
            // constraint. Color.a (pure texIndex in half) can represent up to 2048, but the
            // overlay path limits the atlas to 1024 layers total.
            int texCount = atlasResult.TextureArray != null ? atlasResult.TextureArray.depth : 0;

            UnityEngine.Debug.Assert(texCount <= 1024,
                $"Texture array has {texCount} layers, exceeding the 1024 limit for overlay texture indices in PackedMeshVertex.");

            IContentPhase[] postModPhases =
            {
                new BakeNativePhase(),
                new BuildItemSpritesPhase(),
                new LoadSmeltingRecipesPhase(),
                new RegisterBlockEntitiesPhase(),
                new LoadSoundGroupsPhase(),
            };

            for (int i = 0; i < postModPhases.Length; i++)
            {
                yield return postModPhases[i].Description;
                postModPhases[i].Execute(ctx);
            }

            // ══════════════════════════════════════════════════════════════════
            // Build final result from context
            // ══════════════════════════════════════════════════════════════════

            Result = new ContentPipelineResult(
                ctx.StateRegistry,
                ctx.NativeStateRegistry,
                ctx.NativeAtlasLookup,
                ctx.AtlasResult,
                ctx.BiomeDefinitions,
                ctx.OreDefinitions,
                ctx.ItemEntries,
                ctx.LootTables,
                ctx.TagRegistry,
                ctx.ItemRegistry,
                ctx.CraftingEngine,
                ctx.ItemSpriteAtlas,
                ctx.BlockEntityRegistry,
                ctx.SmeltingRecipeRegistry,
                ctx.DisplayTransformLookup,
                ctx.ToolMaterialRegistry,
                ctx.ToolTraitRegistry,
                ctx.SoundGroupRegistry,
                ctx.ToolPartTextures,
                ctx.ToolMaterials,
                ctx.ToolTemplateRegistry,
                ctx.PartBuilderRecipeRegistry,
                ctx.MaterialInputRegistry);
        }

        /// <summary>Builds the "prop1=val1,prop2=val2" variant key from a state offset.</summary>
        private static string BuildVariantKey(BlockDefinition block, int stateOffset)
        {
            IReadOnlyList<BlockPropertyEntry> properties = block.Properties;

            if (properties == null || properties.Count == 0)
            {
                return "";
            }

            StringBuilder sb = new();
            int remaining = stateOffset;

            for (int i = 0; i < properties.Count; i++)
            {
                BlockPropertyEntry prop = properties[i];
                int valueCount = prop.ValueCount;
                int valueIndex = remaining % valueCount;
                remaining /= valueCount;

                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(prop.Name);
                sb.Append('=');
                sb.Append(prop.GetValue(valueIndex));
            }

            return sb.ToString();
        }

        /// <summary>Finds the variant entry matching the given key in a block state mapping.</summary>
        private static BlockStateVariantEntry FindVariant(
            BlockStateMapping mapping,
            string variantKey)
        {
            IReadOnlyList<BlockStateVariantEntry> variants = mapping.Variants;

            for (int i = 0; i < variants.Count; i++)
            {
                if (string.Equals(variants[i].VariantKey, variantKey, StringComparison.Ordinal))
                {
                    return variants[i];
                }
            }

            return null;
        }

        /// <summary>Returns the atlas index for a texture, or the missing texture index if not found.</summary>
        private static ushort GetTextureIndex(AtlasResult atlas, Texture2D texture)
        {
            if (texture != null && atlas.IndexByTexture.TryGetValue(texture, out int index))
            {
                return (ushort)index;
            }

            return (ushort)atlas.MissingTextureIndex;
        }

        /// <summary>
        ///     Resolves per-face tint types and overlay textures from model elements.
        ///     Element 0 provides base per-face tint. Elements 1..N provide overlay
        ///     textures and overlay per-face tint (last distinct texture wins per face).
        /// </summary>
        private void ResolveOverlaysAndTints(
            ContentModelResolver modelResolver,
            StateRegistry stateRegistry,
            Dictionary<string, BlockDefinition> blockLookup,
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces)
        {
            IReadOnlyList<StateRegistryEntry> entries = stateRegistry.Entries;
            int tintedStates = 0;
            int overlayStates = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (!blockLookup.TryGetValue(entry.Id.ToString(), out BlockDefinition block))
                {
                    continue;
                }

                BlockStateMapping mapping = block.BlockStateMapping;

                if (mapping == null)
                {
                    continue;
                }

                for (int offset = 0; offset < entry.StateCount; offset++)
                {
                    StateId stateId = new((ushort)(entry.BaseStateId + offset));
                    string variantKey = BuildVariantKey(block, offset);
                    BlockStateVariantEntry variant = FindVariant(mapping, variantKey);

                    if (variant == null || variant.Model == null)
                    {
                        continue;
                    }

                    if (!resolvedFaces.TryGetValue(stateId, out ResolvedFaceTextures2D faceData))
                    {
                        continue;
                    }

                    ResolvedModel fullModel = modelResolver.ResolveFull(variant.Model);
                    List<ModelElement> elements = fullModel.Elements;

                    // Resolve per-face tint from element 0 (base)
                    if (elements.Count > 0)
                    {
                        ModelElement baseElem = elements[0];
                        faceData.TintNorth = MapTintIndex(baseElem.North);
                        faceData.TintSouth = MapTintIndex(baseElem.South);
                        faceData.TintEast = MapTintIndex(baseElem.East);
                        faceData.TintWest = MapTintIndex(baseElem.West);
                        faceData.TintUp = MapTintIndex(baseElem.Up);
                        faceData.TintDown = MapTintIndex(baseElem.Down);

                        if (faceData.TintNorth > 0 || faceData.TintSouth > 0 ||
                            faceData.TintEast > 0 || faceData.TintWest > 0 ||
                            faceData.TintUp > 0 || faceData.TintDown > 0)
                        {
                            tintedStates++;
                        }
                    }

                    // Fallback: if no elements produced tint data AND block has a
                    // defaultTintType, apply it uniformly to all faces. This handles
                    // blocks like oak_leaves and water that use builtInParent with no
                    // model elements (Minecraft applies their tint via BlockColors.register()
                    // in code, not via model tintIndex).
                    if (faceData is
                        {
                            TintNorth: 0,
                            TintSouth: 0,
                            TintEast: 0,
                            TintWest: 0,
                            TintUp: 0,
                            TintDown: 0,
                        } &&
                        block.DefaultTintType > 0)
                    {
                        byte dt = (byte)block.DefaultTintType;
                        faceData.TintNorth = dt;
                        faceData.TintSouth = dt;
                        faceData.TintEast = dt;
                        faceData.TintWest = dt;
                        faceData.TintUp = dt;
                        faceData.TintDown = dt;
                        tintedStates++;
                    }

                    // Resolve overlay from elements 1..N (scan all, last distinct texture wins)
                    if (elements.Count > 1)
                    {
                        Dictionary<string, Texture2D> resolvedTextures =
                            fullModel.ResolvedTextureDictionary;
                        ModelElement baseElem = elements[0];

                        ResolveOverlayFromElements(elements, baseElem, resolvedTextures,
                            ref faceData.OverlayNorth, ref faceData.OverlayTintNorth,
                            e => e.North);
                        ResolveOverlayFromElements(elements, baseElem, resolvedTextures,
                            ref faceData.OverlaySouth, ref faceData.OverlayTintSouth,
                            e => e.South);
                        ResolveOverlayFromElements(elements, baseElem, resolvedTextures,
                            ref faceData.OverlayEast, ref faceData.OverlayTintEast,
                            e => e.East);
                        ResolveOverlayFromElements(elements, baseElem, resolvedTextures,
                            ref faceData.OverlayWest, ref faceData.OverlayTintWest,
                            e => e.West);
                        ResolveOverlayFromElements(elements, baseElem, resolvedTextures,
                            ref faceData.OverlayUp, ref faceData.OverlayTintUp,
                            e => e.Up);
                        ResolveOverlayFromElements(elements, baseElem, resolvedTextures,
                            ref faceData.OverlayDown, ref faceData.OverlayTintDown,
                            e => e.Down);

                        if (faceData.OverlayNorth != null || faceData.OverlaySouth != null ||
                            faceData.OverlayEast != null || faceData.OverlayWest != null ||
                            faceData.OverlayUp != null || faceData.OverlayDown != null)
                        {
                            overlayStates++;
                        }
                    }

                    resolvedFaces[stateId] = faceData;
                }
            }

            _logger.LogInfo($"Resolved per-face tints: {tintedStates} tinted, {overlayStates} with overlays.");
        }

        /// <summary>
        ///     Maps a ModelFaceEntry.TintIndex to a per-face tint type byte:
        ///     -1 → 0 (none), 0 → 1 (grass), 1 → 2 (foliage), ≥2 → 3 (water)
        /// </summary>
        private static byte MapTintIndex(ModelFaceEntry face)
        {
            if (face == null)
            {
                return 0;
            }

            int tintIndex = face.TintIndex;

            if (tintIndex == 0)
            {
                return 1; // grass
            }

            if (tintIndex == 1)
            {
                return 2; // foliage
            }

            if (tintIndex >= 2)
            {
                return 3; // water
            }

            return 0; // -1 or absent = no tint
        }

        /// <summary>Resolves an overlay texture and tint type from a single model face entry.</summary>
        private static void ResolveOverlayFace(
            ModelFaceEntry face,
            Dictionary<string, Texture2D> resolvedTextures,
            ref Texture2D overlayTex,
            ref byte overlayTintType)
        {
            if (face == null || string.IsNullOrEmpty(face.Texture))
            {
                return;
            }

            // Resolve #variable reference
            string texRef = face.Texture;

            if (texRef.StartsWith("#"))
            {
                string varName = texRef.Substring(1);

                if (resolvedTextures.TryGetValue(varName, out Texture2D resolved))
                {
                    overlayTex = resolved;
                }
            }

            overlayTintType = MapTintIndex(face);
        }

        /// <summary>
        ///     Scans elements 1..N for the last element whose face has a different texture
        ///     than element 0's face. That element's texture becomes the overlay.
        /// </summary>
        private static void ResolveOverlayFromElements(
            List<ModelElement> elements,
            ModelElement baseElem,
            Dictionary<string, Texture2D> resolvedTextures,
            ref Texture2D overlayTex,
            ref byte overlayTintType,
            Func<ModelElement, ModelFaceEntry> faceSelector)
        {
            ModelFaceEntry baseFace = faceSelector(baseElem);
            string baseTexRef = baseFace?.Texture;

            // Scan from last element backwards — first (= last in list) distinct texture wins
            for (int i = elements.Count - 1; i >= 1; i--)
            {
                ModelFaceEntry candidateFace = faceSelector(elements[i]);

                if (candidateFace == null || string.IsNullOrEmpty(candidateFace.Texture))
                {
                    continue;
                }

                // Skip if same texture reference as base (not an overlay)
                if (candidateFace.Texture == baseTexRef)
                {
                    continue;
                }

                // This element has a different texture for this face — it's the overlay
                ResolveOverlayFace(candidateFace, resolvedTextures,
                    ref overlayTex, ref overlayTintType);
                return;
            }
        }
    }
}
