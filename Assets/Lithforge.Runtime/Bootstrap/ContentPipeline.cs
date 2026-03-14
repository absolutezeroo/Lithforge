using System;
using System.Collections.Generic;
using System.Text;
using Lithforge.Core.Data;
using Lithforge.Core.Validation;
using ILogger = Lithforge.Core.Logging.ILogger;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items;
using Lithforge.Runtime.Content.Items.Affixes;
using Lithforge.Runtime.Content.Loot;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Content.Mods;
using Lithforge.Runtime.Content.Recipes;
using Lithforge.Runtime.Content.Tags;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.Factories;
using Lithforge.Runtime.BlockEntity.ScriptableObjects;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
using Lithforge.Voxel.Tag;
using Unity.Collections;
using UnityEngine;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    /// Orchestrates the full content loading pipeline using ScriptableObjects:
    ///   Phase 1:  Load block definitions via Resources.LoadAll
    ///   Phase 2:  Register blocks in StateRegistry
    ///   Phase 3:  Resolve block models via ContentModelResolver
    ///   Phase 4:  Resolve blockstate variants to per-face textures
    ///   Phase 5:  Build texture atlas
    ///   Phase 6:  Patch texture indices into StateRegistry
    ///   Phase 7:  Load biome and ore definitions
    ///   Phase 8:  Load item definitions
    ///   Phase 9:  Load loot tables
    ///   Phase 10: Load tags and build TagRegistry
    ///   Phase 11: Load recipes and build CraftingEngine
    ///   Phase 12: Build ItemRegistry
    ///   Phase 13: Load mods
    ///   Phase 14: BakeNative (freeze) + build NativeAtlasLookup
    /// </summary>
    public sealed class ContentPipeline
    {
        private readonly ILogger _logger;
        private readonly ContentValidator _validator;
        private readonly int _atlasTileSize;

        /// <summary>
        /// The result of the content pipeline, available after Build() iteration completes.
        /// </summary>
        public ContentPipelineResult Result { get; private set; }

        public ContentPipeline(ILogger logger, ContentValidator validator, int atlasTileSize = 16)
        {
            _logger = logger;
            _validator = validator;
            _atlasTileSize = atlasTileSize;
        }

        /// <summary>
        /// Builds the content pipeline as an iterator, yielding a phase description string
        /// between each processing phase. The final result is stored in the Result property.
        /// Can be consumed from a coroutine to allow frame yields between phases.
        /// </summary>
        public IEnumerable<string> Build()
        {
            // Phase 1: Load block definitions
            yield return "Loading blocks...";
            BlockDefinition[] blocks = Resources.LoadAll<BlockDefinition>("Content/Blocks");
            _logger.LogInfo($"Loaded {blocks.Length} block definitions.");

            // Phase 2: Register blocks in StateRegistry
            yield return "Registering states...";
            StateRegistry stateRegistry = new StateRegistry();
            Dictionary<string, BlockDefinition> blockLookup =
                new Dictionary<string, BlockDefinition>();

            for (int i = 0; i < blocks.Length; i++)
            {
                BlockDefinition block = blocks[i];
                ResourceId id = new ResourceId(block.Namespace, block.BlockName);

                string lootTableStr = null;

                if (block.LootTable != null)
                {
                    lootTableStr = block.LootTable.Namespace + ":" + block.LootTable.TableName;
                }

                BlockRegistrationData regData = new BlockRegistrationData(
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
                    materialType: block.MaterialType,
                    requiredToolLevel: block.RequiredToolLevel);

                stateRegistry.Register(regData);
                blockLookup[id.ToString()] = block;
            }

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
            ContentModelResolver modelResolver = new ContentModelResolver();
            Dictionary<BlockModel, ResolvedFaceTextures2D> resolvedModelCache =
                new Dictionary<BlockModel, ResolvedFaceTextures2D>();

            // Phase 4: Resolve blockstate variants to per-face textures
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces =
                new Dictionary<StateId, ResolvedFaceTextures2D>();

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
                    StateId stateId = new StateId((ushort)(entry.BaseStateId + offset));
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
            AtlasBuilder atlasBuilder = new AtlasBuilder(_logger, _atlasTileSize);
            AtlasResult atlasResult = atlasBuilder.Build(resolvedFaces);

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

            // Phase 7: Load biome and ore definitions
            yield return "Loading biomes and ores...";
            BiomeDefinition[] biomes = Resources.LoadAll<BiomeDefinition>("Content/Biomes");
            _logger.LogInfo($"Loaded {biomes.Length} biome definitions.");

            OreDefinition[] ores = Resources.LoadAll<OreDefinition>("Content/Ores");
            _logger.LogInfo($"Loaded {ores.Length} ore definitions.");

            // Phase 8: Load item definitions
            yield return "Loading items...";
            ItemDefinition[] items = Resources.LoadAll<ItemDefinition>("Content/Items");
            _logger.LogInfo($"Loaded {items.Length} item definitions.");

            // Phase 9: Load loot tables and build lookup
            yield return "Loading loot tables...";
            LootTable[] lootTableAssets = Resources.LoadAll<LootTable>("Content/LootTables");
            Dictionary<ResourceId, LootTableDefinition> lootTables =
                new Dictionary<ResourceId, LootTableDefinition>();

            for (int i = 0; i < lootTableAssets.Length; i++)
            {
                LootTable lt = lootTableAssets[i];
                ResourceId ltId = new ResourceId(lt.Namespace, lt.TableName);
                LootTableDefinition ltDef = ConvertLootTable(lt, ltId);
                lootTables[ltId] = ltDef;
            }

            _logger.LogInfo($"Loaded {lootTables.Count} loot tables.");

            // Phase 10: Load tags and build TagRegistry
            yield return "Loading tags...";
            Tag[] tagAssets = Resources.LoadAll<Tag>("Content/Tags");
            TagRegistry tagRegistry = new TagRegistry();

            for (int i = 0; i < tagAssets.Length; i++)
            {
                Tag tag = tagAssets[i];
                ResourceId tagId = new ResourceId(tag.Namespace, tag.TagName);
                TagDefinition tagDef = new TagDefinition(tagId);
                tagDef.Replace = tag.Replace;

                IReadOnlyList<string> entryIds = tag.EntryIds;

                for (int e = 0; e < entryIds.Count; e++)
                {
                    tagDef.Values.Add(entryIds[e]);
                }

                tagRegistry.Register(tagDef);
            }

            _logger.LogInfo($"Loaded {tagAssets.Length} tags, {tagRegistry.TagCount} unique.");

            // Phase 11: Load recipes and build CraftingEngine
            yield return "Loading recipes...";
            RecipeDefinition[] recipeAssets =
                Resources.LoadAll<RecipeDefinition>("Content/Recipes");
            List<RecipeEntry> recipes = new List<RecipeEntry>();

            for (int i = 0; i < recipeAssets.Length; i++)
            {
                RecipeEntry recipeDef = ConvertRecipe(recipeAssets[i]);
                recipes.Add(recipeDef);
            }

            CraftingEngine craftingEngine = new CraftingEngine(recipes);
            _logger.LogInfo($"Loaded {recipes.Count} crafting recipes.");

            // Phase 12: Build ItemRegistry
            yield return "Building item registry...";
            List<ItemEntry> itemEntries = new List<ItemEntry>();

            for (int i = 0; i < items.Length; i++)
            {
                ItemEntry itemDef = ConvertItem(items[i]);
                itemEntries.Add(itemDef);
            }

            ItemRegistry itemRegistry = new ItemRegistry();
            itemRegistry.RegisterBlockItems(stateRegistry.Entries);
            itemRegistry.RegisterItems(itemEntries);
            _logger.LogInfo($"ItemRegistry: {itemRegistry.Count} items total.");

            // Phase 13: Load mods
            yield return "Loading mods...";
            ModLoader modLoader = new ModLoader();
            modLoader.LoadAllMods();

            for (int i = 0; i < modLoader.LoadedBlocks.Count; i++)
            {
                BlockDefinition modBlock = modLoader.LoadedBlocks[i];
                ResourceId modId = new ResourceId(modBlock.Namespace, modBlock.BlockName);

                string modLootStr = null;

                if (modBlock.LootTable != null)
                {
                    modLootStr = modBlock.LootTable.Namespace + ":" + modBlock.LootTable.TableName;
                }

                BlockRegistrationData modRegData = new BlockRegistrationData(
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
                    materialType: modBlock.MaterialType,
                    requiredToolLevel: modBlock.RequiredToolLevel);

                stateRegistry.Register(modRegData);
            }

            // TintOverlay.overlayTexIndex uses 10 bits (max index 1023), which is the binding
            // constraint. Color.a (pure texIndex in half) can represent up to 2048, but the
            // overlay path limits the atlas to 1024 layers total.
            int texCount = atlasResult.TextureArray != null ? atlasResult.TextureArray.depth : 0;

            UnityEngine.Debug.Assert(texCount <= 1024,
                $"Texture array has {texCount} layers, exceeding the 1024 limit for overlay texture indices in PackedMeshVertex.");

            // Phase 14: BakeNative + build NativeAtlasLookup
            yield return "Baking native data...";
            NativeStateRegistry nativeStateRegistry = stateRegistry.BakeNative(Allocator.Persistent);
            NativeAtlasLookup nativeAtlasLookup = BakeAtlasLookup(stateRegistry, atlasResult, resolvedFaces);

            // Phase 15: Build item sprite atlas for UI
            yield return "Building item sprites...";
            ItemSpriteAtlas itemSpriteAtlas = ItemSpriteAtlasBuilder.Build(
                itemEntries, stateRegistry, resolvedFaces);
            _logger.LogInfo($"Built item sprite atlas: {itemSpriteAtlas.Count} sprites.");

            // Phase 16: Load smelting recipes and register block entity factories
            yield return "Loading smelting recipes...";
            SmeltingRecipeRegistry smeltingRecipeRegistry = new SmeltingRecipeRegistry();
            SmeltingRecipeDefinition[] smeltingRecipes =
                Resources.LoadAll<SmeltingRecipeDefinition>("Content/Recipes/Smelting");

            for (int i = 0; i < smeltingRecipes.Length; i++)
            {
                SmeltingRecipeDefinition sr = smeltingRecipes[i];

                if (!string.IsNullOrEmpty(sr.InputItemId) && !string.IsNullOrEmpty(sr.ResultItemId))
                {
                    ResourceId inputId = ResourceId.Parse(sr.InputItemId);
                    ResourceId resultId = ResourceId.Parse(sr.ResultItemId);
                    SmeltingRecipeEntry entry = new SmeltingRecipeEntry(
                        inputId, resultId, sr.ResultCount, sr.ExperienceReward);
                    smeltingRecipeRegistry.Register(entry);
                }
            }

            _logger.LogInfo($"Loaded {smeltingRecipeRegistry.Count} smelting recipes.");

            // Register block entity factories
            BlockEntityRegistry blockEntityRegistry = new BlockEntityRegistry();
            blockEntityRegistry.Register(new BlockEntityType(
                ChestBlockEntity.TypeIdValue,
                new ChestBlockEntityFactory()));
            blockEntityRegistry.Register(new BlockEntityType(
                FurnaceBlockEntity.TypeIdValue,
                new FurnaceBlockEntityFactory(smeltingRecipeRegistry, itemRegistry)));
            blockEntityRegistry.Freeze();

            _logger.LogInfo($"Registered {blockEntityRegistry.Count} block entity types.");

            Result = new ContentPipelineResult(
                stateRegistry,
                nativeStateRegistry,
                nativeAtlasLookup,
                atlasResult,
                biomes,
                ores,
                itemEntries,
                lootTables,
                tagRegistry,
                itemRegistry,
                craftingEngine,
                itemSpriteAtlas,
                blockEntityRegistry,
                smeltingRecipeRegistry);
        }

        private static string BuildVariantKey(BlockDefinition block, int stateOffset)
        {
            IReadOnlyList<BlockPropertyEntry> properties = block.Properties;

            if (properties == null || properties.Count == 0)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
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

        private static BlockStateVariantEntry FindVariant(
            BlockStateMapping mapping,
            string variantKey)
        {
            IReadOnlyList<BlockStateVariantEntry> variants = mapping.Variants;

            for (int i = 0; i < variants.Count; i++)
            {
                if (string.Equals(variants[i].VariantKey, variantKey, System.StringComparison.Ordinal))
                {
                    return variants[i];
                }
            }

            return null;
        }

        private static LootTableDefinition ConvertLootTable(LootTable lt, ResourceId id)
        {
            LootTableDefinition def = new LootTableDefinition(id);
            def.Type = lt.Type;

            IReadOnlyList<LootPoolEntry> pools = lt.Pools;

            for (int p = 0; p < pools.Count; p++)
            {
                LootPoolEntry poolEntry = pools[p];
                LootPool pool = new LootPool();
                pool.RollsMin = poolEntry.RollsMin;
                pool.RollsMax = poolEntry.RollsMax;

                IReadOnlyList<LootItemEntry> items = poolEntry.Entries;

                for (int e = 0; e < items.Count; e++)
                {
                    LootItemEntry itemEntry = items[e];
                    LootEntry entry = new LootEntry();
                    entry.Type = itemEntry.Type;
                    entry.Name = itemEntry.ItemName;
                    entry.Weight = itemEntry.Weight;

                    IReadOnlyList<LootFunctionEntry> funcs = itemEntry.Functions;

                    for (int f = 0; f < funcs.Count; f++)
                    {
                        LootFunctionEntry funcEntry = funcs[f];
                        LootFunction func = new LootFunction();
                        func.Type = funcEntry.FunctionType;

                        IReadOnlyList<StringPair> pars = funcEntry.Parameters;

                        for (int pi = 0; pi < pars.Count; pi++)
                        {
                            func.Parameters[pars[pi].Key] = pars[pi].Value;
                        }

                        func.PreParseValues();
                        entry.Functions.Add(func);
                    }

                    pool.Entries.Add(entry);
                }

                def.Pools.Add(pool);
            }

            return def;
        }

        private static RecipeEntry ConvertRecipe(RecipeDefinition source)
        {
            ResourceId id = new ResourceId(source.Namespace, source.RecipeName);
            RecipeEntry recipe = new RecipeEntry(id);
            recipe.Type = source.Type;
            recipe.ResultCount = source.ResultCount;

            if (!string.IsNullOrEmpty(source.ResultItemId))
            {
                recipe.ResultItem = ResourceId.Parse(source.ResultItemId);
            }

            IReadOnlyList<string> pattern = source.Pattern;

            for (int i = 0; i < pattern.Count; i++)
            {
                recipe.Pattern.Add(pattern[i]);
            }

            IReadOnlyList<RecipeKeyEntry> keys = source.Keys;

            for (int i = 0; i < keys.Count; i++)
            {
                RecipeKeyEntry key = keys[i];

                if (!string.IsNullOrEmpty(key.ItemId))
                {
                    recipe.Keys[key.Key] = ResourceId.Parse(key.ItemId);
                }
            }

            IReadOnlyList<RecipeIngredient> ingredients = source.Ingredients;

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!string.IsNullOrEmpty(ingredients[i].ItemId))
                {
                    recipe.Ingredients.Add(ResourceId.Parse(ingredients[i].ItemId));
                }
            }

            return recipe;
        }

        private static ItemEntry ConvertItem(ItemDefinition item)
        {
            ResourceId id = new ResourceId(item.Namespace, item.ItemName);
            ItemEntry def = new ItemEntry(id);
            def.MaxStackSize = item.MaxStackSize;
            def.ToolType = item.ToolType;
            def.ToolLevel = item.ToolLevel;
            def.Durability = item.Durability;
            def.AttackDamage = item.AttackDamage;
            def.AttackSpeed = item.AttackSpeed;
            def.MiningSpeed = item.MiningSpeed;

            def.FuelTime = item.FuelTime;
            def.ToolSpeedProfile = item.ToolSpeedProfile;

            AffixDefinition[] sourceAffixes = item.Affixes ?? Array.Empty<AffixDefinition>();
            List<IMiningModifier> modList = new List<IMiningModifier>(sourceAffixes.Length);

            for (int a = 0; a < sourceAffixes.Length; a++)
            {
                if (sourceAffixes[a] != null)
                {
                    modList.Add(sourceAffixes[a]);
                }
            }

            IMiningModifier[] mods = modList.ToArray();
            Array.Sort(mods, (x, y) => x.Priority.CompareTo(y.Priority));
            def.Modifiers = mods;

            if (item.PlacesBlock != null)
            {
                def.IsBlockItem = true;
                def.BlockId = new ResourceId(
                    item.PlacesBlock.Namespace, item.PlacesBlock.BlockName);
            }

            IReadOnlyList<string> tags = item.Tags;

            for (int i = 0; i < tags.Count; i++)
            {
                def.Tags.Add(tags[i]);
            }

            return def;
        }

        /// <summary>
        /// Resolves per-face tint types and overlay textures from model elements.
        /// Element 0 provides base per-face tint. Elements 1..N provide overlay
        /// textures and overlay per-face tint (last distinct texture wins per face).
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
                    StateId stateId = new StateId((ushort)(entry.BaseStateId + offset));
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
                    if (faceData.TintNorth == 0 && faceData.TintSouth == 0 &&
                        faceData.TintEast == 0 && faceData.TintWest == 0 &&
                        faceData.TintUp == 0 && faceData.TintDown == 0 &&
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
        /// Maps a ModelFaceEntry.TintIndex to a per-face tint type byte:
        ///   -1 → 0 (none), 0 → 1 (grass), 1 → 2 (foliage), ≥2 → 3 (water)
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
        /// Scans elements 1..N for the last element whose face has a different texture
        /// than element 0's face. That element's texture becomes the overlay.
        /// </summary>
        private static void ResolveOverlayFromElements(
            List<ModelElement> elements,
            ModelElement baseElem,
            Dictionary<string, Texture2D> resolvedTextures,
            ref Texture2D overlayTex,
            ref byte overlayTintType,
            System.Func<ModelElement, ModelFaceEntry> faceSelector)
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

        private static ushort GetTextureIndex(AtlasResult atlas, Texture2D texture)
        {
            if (texture != null && atlas.IndexByTexture.TryGetValue(texture, out int index))
            {
                return (ushort)index;
            }

            return (ushort)atlas.MissingTextureIndex;
        }

        private static NativeAtlasLookup BakeAtlasLookup(
            StateRegistry stateRegistry,
            AtlasResult atlasResult,
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces)
        {
            int totalStates = stateRegistry.TotalStateCount;
            NativeArray<AtlasEntry> entries = new NativeArray<AtlasEntry>(
                totalStates, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < totalStates; i++)
            {
                StateId sid = new StateId((ushort)i);
                BlockStateCompact state = stateRegistry.GetState(sid);

                AtlasEntry entry = new AtlasEntry
                {
                    // Base texture indices (from StateRegistry, already patched)
                    TexPosX = state.TexEast,
                    TexNegX = state.TexWest,
                    TexPosY = state.TexUp,
                    TexNegY = state.TexDown,
                    TexPosZ = state.TexSouth,
                    TexNegZ = state.TexNorth,

                    // Defaults: no overlay
                    OvlPosX = 0xFFFF,
                    OvlNegX = 0xFFFF,
                    OvlPosY = 0xFFFF,
                    OvlNegY = 0xFFFF,
                    OvlPosZ = 0xFFFF,
                    OvlNegZ = 0xFFFF,

                    BaseTintPacked = 0,
                    OverlayTintPacked = 0,
                };

                // Populate overlay + per-face tint from resolved face data
                if (resolvedFaces.TryGetValue(sid, out ResolvedFaceTextures2D faces))
                {
                    // Per-face base tint (face direction: PosX=East, NegX=West, PosY=Up, NegY=Down, PosZ=South, NegZ=North)
                    entry.BaseTintPacked = PackFaceTints(
                        faces.TintEast, faces.TintWest,
                        faces.TintUp, faces.TintDown,
                        faces.TintSouth, faces.TintNorth);

                    // Overlay textures
                    entry.OvlPosX = GetOverlayIndex(atlasResult, faces.OverlayEast);
                    entry.OvlNegX = GetOverlayIndex(atlasResult, faces.OverlayWest);
                    entry.OvlPosY = GetOverlayIndex(atlasResult, faces.OverlayUp);
                    entry.OvlNegY = GetOverlayIndex(atlasResult, faces.OverlayDown);
                    entry.OvlPosZ = GetOverlayIndex(atlasResult, faces.OverlaySouth);
                    entry.OvlNegZ = GetOverlayIndex(atlasResult, faces.OverlayNorth);

                    // Per-face overlay tint
                    entry.OverlayTintPacked = PackFaceTints(
                        faces.OverlayTintEast, faces.OverlayTintWest,
                        faces.OverlayTintUp, faces.OverlayTintDown,
                        faces.OverlayTintSouth, faces.OverlayTintNorth);
                }

                entries[i] = entry;
            }

            int textureCount = 0;

            if (atlasResult.TextureArray != null)
            {
                textureCount = atlasResult.TextureArray.depth;
            }

            return new NativeAtlasLookup(entries, textureCount);
        }

        private static ushort PackFaceTints(
            byte posX, byte negX, byte posY, byte negY, byte posZ, byte negZ)
        {
            return (ushort)(
                (posX & 0x3) |
                ((negX & 0x3) << 2) |
                ((posY & 0x3) << 4) |
                ((negY & 0x3) << 6) |
                ((posZ & 0x3) << 8) |
                ((negZ & 0x3) << 10));
        }

        private static ushort GetOverlayIndex(AtlasResult atlas, Texture2D texture)
        {
            if (texture != null && atlas.IndexByTexture.TryGetValue(texture, out int index))
            {
                return (ushort)index;
            }

            return 0xFFFF;
        }
    }
}
