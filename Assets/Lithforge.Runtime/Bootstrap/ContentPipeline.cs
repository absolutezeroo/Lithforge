using System.Collections.Generic;
using System.Text;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
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
    ///   Phase 1:  Load block SOs via Resources.LoadAll
    ///   Phase 2:  Register blocks in StateRegistry
    ///   Phase 3:  Resolve block models via SOModelResolver
    ///   Phase 4:  Resolve blockstate variants to per-face textures
    ///   Phase 5:  Build texture atlas
    ///   Phase 6:  Patch texture indices into StateRegistry
    ///   Phase 7:  Load biome and ore SOs
    ///   Phase 8:  Load item SOs
    ///   Phase 9:  Load loot table SOs
    ///   Phase 10: Load tag SOs and build TagRegistry
    ///   Phase 11: Load recipe SOs and build CraftingEngine
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
            // Phase 1: Load block SOs
            BlockDefinitionSO[] blockSOs = Resources.LoadAll<BlockDefinitionSO>("Content/Blocks");
            _logger.LogInfo($"Loaded {blockSOs.Length} block ScriptableObjects.");

            // Phase 2: Register blocks in StateRegistry
            StateRegistry stateRegistry = new StateRegistry();
            Dictionary<string, BlockDefinitionSO> blockSOLookup =
                new Dictionary<string, BlockDefinitionSO>();

            for (int i = 0; i < blockSOs.Length; i++)
            {
                BlockDefinitionSO blockSO = blockSOs[i];
                ResourceId id = new ResourceId(blockSO.Namespace, blockSO.BlockName);

                string lootTableStr = null;

                if (blockSO.LootTable != null)
                {
                    lootTableStr = blockSO.LootTable.Namespace + ":" + blockSO.LootTable.TableName;
                }

                BlockRegistrationData regData = new BlockRegistrationData(
                    id,
                    blockSO.ComputeStateCount(),
                    blockSO.RenderLayerString,
                    blockSO.CollisionShapeString,
                    blockSO.LightEmission,
                    blockSO.LightFilter,
                    blockSO.MapColor,
                    lootTableStr,
                    (float)blockSO.Hardness,
                    (float)blockSO.BlastResistance);

                stateRegistry.Register(regData);
                blockSOLookup[id.ToString()] = blockSO;
            }

            _logger.LogInfo(
                $"Registered {blockSOs.Length} blocks, {stateRegistry.TotalStateCount} states.");

            // Phase 3: Resolve block models via SOModelResolver
            SOModelResolver modelResolver = new SOModelResolver();
            Dictionary<BlockModelSO, ResolvedFaceTextures> resolvedModelCache =
                new Dictionary<BlockModelSO, ResolvedFaceTextures>();

            // Phase 4: Resolve blockstate variants to per-face textures
            Dictionary<StateId, ResolvedFaceTextures> resolvedFaces =
                new Dictionary<StateId, ResolvedFaceTextures>();

            IReadOnlyList<StateRegistryEntry> entries = stateRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (!blockSOLookup.TryGetValue(entry.Id.ToString(), out BlockDefinitionSO blockSO))
                {
                    continue;
                }

                BlockStateMappingSO mapping = blockSO.BlockStateMapping;

                if (mapping == null)
                {
                    _logger.LogWarning($"No BlockStateMapping for '{entry.Id}'.");
                    continue;
                }

                for (int offset = 0; offset < entry.StateCount; offset++)
                {
                    StateId stateId = new StateId((ushort)(entry.BaseStateId + offset));
                    string variantKey = BuildVariantKey(blockSO, offset);

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

            // Phase 7: Load biome and ore SOs
            BiomeDefinitionSO[] biomeSOs = Resources.LoadAll<BiomeDefinitionSO>("Content/Biomes");
            _logger.LogInfo($"Loaded {biomeSOs.Length} biome definitions.");

            OreDefinitionSO[] oreSOs = Resources.LoadAll<OreDefinitionSO>("Content/Ores");
            _logger.LogInfo($"Loaded {oreSOs.Length} ore definitions.");

            // Phase 8: Load item SOs
            ItemDefinitionSO[] itemSOs = Resources.LoadAll<ItemDefinitionSO>("Content/Items");
            _logger.LogInfo($"Loaded {itemSOs.Length} item definitions.");

            // Phase 9: Load loot table SOs and build lookup
            LootTableSO[] lootTableSOs = Resources.LoadAll<LootTableSO>("Content/LootTables");
            Dictionary<ResourceId, LootTableDefinition> lootTables =
                new Dictionary<ResourceId, LootTableDefinition>();

            for (int i = 0; i < lootTableSOs.Length; i++)
            {
                LootTableSO ltSO = lootTableSOs[i];
                ResourceId ltId = new ResourceId(ltSO.Namespace, ltSO.TableName);
                LootTableDefinition ltDef = ConvertLootTable(ltSO, ltId);
                lootTables[ltId] = ltDef;
            }

            _logger.LogInfo($"Loaded {lootTables.Count} loot tables.");

            // Phase 10: Load tag SOs and build TagRegistry
            TagSO[] tagSOs = Resources.LoadAll<TagSO>("Content/Tags");
            TagRegistry tagRegistry = new TagRegistry();

            for (int i = 0; i < tagSOs.Length; i++)
            {
                TagSO tagSO = tagSOs[i];
                ResourceId tagId = new ResourceId(tagSO.Namespace, tagSO.TagName);
                TagDefinition tagDef = new TagDefinition(tagId);
                tagDef.Replace = tagSO.Replace;

                IReadOnlyList<string> entryIds = tagSO.EntryIds;

                for (int e = 0; e < entryIds.Count; e++)
                {
                    tagDef.Values.Add(entryIds[e]);
                }

                tagRegistry.Register(tagDef);
            }

            _logger.LogInfo($"Loaded {tagSOs.Length} tags, {tagRegistry.TagCount} unique.");

            // Phase 11: Load recipe SOs and build CraftingEngine
            RecipeDefinitionSO[] recipeSOs =
                Resources.LoadAll<RecipeDefinitionSO>("Content/Recipes");
            List<RecipeDefinition> recipes = new List<RecipeDefinition>();

            for (int i = 0; i < recipeSOs.Length; i++)
            {
                RecipeDefinition recipeDef = ConvertRecipe(recipeSOs[i]);
                recipes.Add(recipeDef);
            }

            CraftingEngine craftingEngine = new CraftingEngine(recipes);
            _logger.LogInfo($"Loaded {recipes.Count} crafting recipes.");

            // Phase 12: Build ItemRegistry
            List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

            for (int i = 0; i < itemSOs.Length; i++)
            {
                ItemDefinition itemDef = ConvertItem(itemSOs[i]);
                itemDefinitions.Add(itemDef);
            }

            ItemRegistry itemRegistry = new ItemRegistry();
            itemRegistry.RegisterBlockItems(stateRegistry.Entries);
            itemRegistry.RegisterItems(itemDefinitions);
            _logger.LogInfo($"ItemRegistry: {itemRegistry.Count} items total.");

            // Phase 13: Load mods
            ModLoader modLoader = new ModLoader();
            modLoader.LoadAllMods();

            for (int i = 0; i < modLoader.LoadedBlocks.Count; i++)
            {
                BlockDefinitionSO modBlock = modLoader.LoadedBlocks[i];
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
                biomeSOs,
                oreSOs,
                itemDefinitions,
                lootTables,
                tagRegistry,
                itemRegistry,
                craftingEngine);
        }

        private static string BuildVariantKey(BlockDefinitionSO blockSO, int stateOffset)
        {
            IReadOnlyList<BlockPropertyEntry> properties = blockSO.Properties;

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
            BlockStateMappingSO mapping,
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

        private static LootTableDefinition ConvertLootTable(LootTableSO ltSO, ResourceId id)
        {
            LootTableDefinition def = new LootTableDefinition(id);
            def.Type = ltSO.Type;

            IReadOnlyList<LootPoolEntry> pools = ltSO.Pools;

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

        private static RecipeDefinition ConvertRecipe(RecipeDefinitionSO recipeSO)
        {
            ResourceId id = new ResourceId(recipeSO.Namespace, recipeSO.RecipeName);
            RecipeDefinition recipe = new RecipeDefinition(id);
            recipe.Type = recipeSO.Type;
            recipe.ResultCount = recipeSO.ResultCount;

            if (!string.IsNullOrEmpty(recipeSO.ResultItemId))
            {
                recipe.ResultItem = ResourceId.Parse(recipeSO.ResultItemId);
            }

            IReadOnlyList<string> pattern = recipeSO.Pattern;

            for (int i = 0; i < pattern.Count; i++)
            {
                recipe.Pattern.Add(pattern[i]);
            }

            IReadOnlyList<RecipeKeyEntry> keys = recipeSO.Keys;

            for (int i = 0; i < keys.Count; i++)
            {
                RecipeKeyEntry key = keys[i];

                if (!string.IsNullOrEmpty(key.ItemId))
                {
                    recipe.Keys[key.Key] = ResourceId.Parse(key.ItemId);
                }
            }

            IReadOnlyList<RecipeIngredient> ingredients = recipeSO.Ingredients;

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!string.IsNullOrEmpty(ingredients[i].ItemId))
                {
                    recipe.Ingredients.Add(ResourceId.Parse(ingredients[i].ItemId));
                }
            }

            return recipe;
        }

        private static ItemDefinition ConvertItem(ItemDefinitionSO itemSO)
        {
            ResourceId id = new ResourceId(itemSO.Namespace, itemSO.ItemName);
            ItemDefinition def = new ItemDefinition(id);
            def.MaxStackSize = itemSO.MaxStackSize;
            def.ToolType = itemSO.ToolType;
            def.ToolLevel = itemSO.ToolLevel;
            def.Durability = itemSO.Durability;
            def.AttackDamage = itemSO.AttackDamage;
            def.AttackSpeed = itemSO.AttackSpeed;
            def.MiningSpeed = itemSO.MiningSpeed;

            if (itemSO.PlacesBlock != null)
            {
                def.IsBlockItem = true;
                def.BlockId = new ResourceId(
                    itemSO.PlacesBlock.Namespace, itemSO.PlacesBlock.BlockName);
            }

            IReadOnlyList<string> tags = itemSO.Tags;

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
