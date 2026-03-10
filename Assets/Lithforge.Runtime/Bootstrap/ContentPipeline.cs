using System.Collections.Generic;
using System.Text;
using Lithforge.Core.Data;
using Lithforge.Core.Validation;
using ILogger = Lithforge.Core.Logging.ILogger;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;
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

        public ContentPipeline(ILogger logger, ContentValidator validator)
        {
            _logger = logger;
            _validator = validator;
        }

        public ContentPipelineResult Build(string contentRoot)
        {
            // Phase 1: Load block definitions
            BlockDefinition[] blocks = Resources.LoadAll<BlockDefinition>("Content/Blocks");
            _logger.LogInfo($"Loaded {blocks.Length} block definitions.");

            // Phase 2: Register blocks in StateRegistry
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
                    (float)block.BlastResistance);

                stateRegistry.Register(regData);
                blockLookup[id.ToString()] = block;
            }

            _logger.LogInfo(
                $"Registered {blocks.Length} blocks, {stateRegistry.TotalStateCount} states.");

            // Phase 3: Resolve block models via ContentModelResolver
            ContentModelResolver modelResolver = new ContentModelResolver();
            Dictionary<BlockModel, ResolvedFaceTextures> resolvedModelCache =
                new Dictionary<BlockModel, ResolvedFaceTextures>();

            // Phase 4: Resolve blockstate variants to per-face textures
            Dictionary<StateId, ResolvedFaceTextures> resolvedFaces =
                new Dictionary<StateId, ResolvedFaceTextures>();

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

                    if (!resolvedModelCache.TryGetValue(variant.Model, out ResolvedFaceTextures faces))
                    {
                        faces = modelResolver.Resolve(variant.Model);
                        resolvedModelCache[variant.Model] = faces;
                    }

                    resolvedFaces[stateId] = faces;
                }
            }

            _logger.LogInfo($"Resolved {resolvedFaces.Count} block state face textures.");

            // Phase 5: Build texture atlas
            AtlasBuilder atlasBuilder = new AtlasBuilder(_logger);
            AtlasResult atlasResult = atlasBuilder.Build(resolvedFaces, contentRoot);

            // Phase 6: Patch texture indices into StateRegistry
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

            // Phase 7: Load biome and ore definitions
            BiomeDefinition[] biomes = Resources.LoadAll<BiomeDefinition>("Content/Biomes");
            _logger.LogInfo($"Loaded {biomes.Length} biome definitions.");

            OreDefinition[] ores = Resources.LoadAll<OreDefinition>("Content/Ores");
            _logger.LogInfo($"Loaded {ores.Length} ore definitions.");

            // Phase 8: Load item definitions
            ItemDefinition[] items = Resources.LoadAll<ItemDefinition>("Content/Items");
            _logger.LogInfo($"Loaded {items.Length} item definitions.");

            // Phase 9: Load loot tables and build lookup
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
                    (float)modBlock.BlastResistance);

                stateRegistry.Register(modRegData);
            }

            // Phase 14: BakeNative + build NativeAtlasLookup
            NativeStateRegistry nativeStateRegistry = stateRegistry.BakeNative(Allocator.Persistent);
            NativeAtlasLookup nativeAtlasLookup = BakeAtlasLookup(stateRegistry, atlasResult);

            return new ContentPipelineResult(
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
                craftingEngine);
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
