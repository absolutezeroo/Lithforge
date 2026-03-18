using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.UI.Sprites;
using Lithforge.Voxel.Block;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class BuildItemSpritesPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Building item sprites...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            // Phase 15: Build tool part texture database + item sprite atlas
            ToolPartTextureDatabase toolTexDb = new(ctx.ToolDefinitions, ctx.ToolMaterials);
            ItemSpriteAtlas itemSpriteAtlas = ItemSpriteAtlasBuilder.Build(
                ctx.ItemEntries, ctx.StateRegistry, ctx.ResolvedFaces, toolTexDb);
            ctx.ToolPartTextures = toolTexDb;
            ctx.ItemSpriteAtlas = itemSpriteAtlas;
            ctx.Logger.LogInfo($"Built item sprite atlas: {itemSpriteAtlas.Count} sprites.");

            // Phase 15.5: Build item display transform lookup for first-person held items
            ItemDisplayTransformLookup displayTransformLookup = new();

            // Load base model assets for fallback display transforms
            BlockModel blockBaseModel = Resources.Load<BlockModel>("Content/Models/block");
            BlockModel generatedBaseModel = Resources.Load<BlockModel>("Content/ItemModels/generated");

            ModelDisplayTransform blockBaseDt = blockBaseModel != null
                ? ctx.ModelResolver.ResolveFirstPersonRightHand(blockBaseModel)
                : null;
            ModelDisplayTransform generatedBaseDt = generatedBaseModel != null
                ? ctx.ModelResolver.ResolveFirstPersonRightHand(generatedBaseModel)
                : null;

            // Block items: resolve display transform from block variant model chain
            IReadOnlyList<StateRegistryEntry> entries = ctx.StateRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (entry.BaseStateId == 0)
                {
                    continue;
                }

                if (!ctx.BlockLookup.TryGetValue(entry.Id.ToString(), out BlockDefinition block))
                {
                    continue;
                }

                BlockStateMapping mapping = block.BlockStateMapping;

                if (mapping == null || mapping.Variants.Count == 0)
                {
                    continue;
                }

                BlockModel variantModel = mapping.Variants[0].Model;
                ModelDisplayTransform dt = ctx.ModelResolver.ResolveFirstPersonRightHand(variantModel);

                // Fallback to block.asset display transform if model chain has none
                if (dt == null)
                {
                    dt = blockBaseDt;
                }

                if (dt != null)
                {
                    displayTransformLookup.Register(entry.Id,
                        ItemDisplayTransformLookup.BuildMatrix(dt));
                }
            }

            // Standalone items: resolve display transform from item model chain
            for (int i = 0; i < ctx.Items.Length; i++)
            {
                ItemDefinition item = ctx.Items[i];

                if (item.ItemModel == null)
                {
                    continue;
                }

                ModelDisplayTransform dt = ctx.ModelResolver.ResolveFirstPersonRightHand(item.ItemModel);

                // Fallback to generated.asset display transform if model chain has none
                if (dt == null)
                {
                    dt = generatedBaseDt;
                }

                if (dt != null)
                {
                    ResourceId itemId = new(item.Namespace, item.ItemName);
                    displayTransformLookup.Register(itemId,
                        ItemDisplayTransformLookup.BuildMatrix(dt));
                }
            }

            ctx.DisplayTransformLookup = displayTransformLookup;
        }
    }
}
