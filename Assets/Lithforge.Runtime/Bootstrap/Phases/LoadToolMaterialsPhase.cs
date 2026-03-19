using System;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Voxel.Crafting;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Loads ToolMaterialDefinition ScriptableObjects and builds both ToolMaterialRegistry and MaterialInputRegistry.</summary>
    public sealed class LoadToolMaterialsPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading tool materials...";
            }
        }

        /// <summary>Loads tool material assets, builds the material registry, and populates the material input registry for part crafting.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            ctx.ToolMaterials =
                Resources.LoadAll<ToolMaterialDefinition>("Content/ToolMaterials");
            ToolMaterialRegistry toolMaterialRegistry = new();

            for (int i = 0; i < ctx.ToolMaterials.Length; i++)
            {
                ToolMaterialDefinition mat = ctx.ToolMaterials[i];

                if (string.IsNullOrEmpty(mat.materialId))
                {
                    continue;
                }

                if (!ResourceId.TryParse(mat.materialId, out ResourceId matId))
                {
                    ctx.Logger.LogWarning($"Invalid tool material id: {mat.materialId}");
                    continue;
                }

                ToolMaterialData matData = new(
                    matId,
                    mat.compatibleParts ?? Array.Empty<ToolPartType>(),
                    mat.headMiningSpeed,
                    mat.headDurability,
                    mat.headAttackDamage,
                    mat.handleDurabilityMultiplier,
                    mat.handleSpeedMultiplier,
                    mat.bindingDurabilityBonus,
                    mat.traitIds ?? Array.Empty<string>(),
                    mat.toolLevel,
                    mat.isCraftable,
                    mat.partBuilderCost);

                toolMaterialRegistry.Register(matData);
            }

            ctx.ToolMaterialRegistry = toolMaterialRegistry;
            ctx.Logger.LogInfo($"Loaded {toolMaterialRegistry.Count} tool materials.");

            // Build MaterialInputRegistry (TiC-style value/needed/leftover per item)
            MaterialInputRegistry materialInputRegistry = new();

            for (int i = 0; i < ctx.ToolMaterials.Length; i++)
            {
                ToolMaterialDefinition def = ctx.ToolMaterials[i];

                if (!def.isCraftable)
                {
                    continue;
                }

                if (def.materialInputs == null || def.materialInputs.Length == 0)
                {
                    continue;
                }

                if (!ResourceId.TryParse(def.materialId, out ResourceId materialId))
                {
                    continue;
                }

                for (int j = 0; j < def.materialInputs.Length; j++)
                {
                    MaterialInputEntry entry = def.materialInputs[j];

                    if (string.IsNullOrEmpty(entry.itemId))
                    {
                        continue;
                    }

                    if (!ResourceId.TryParse(entry.itemId, out ResourceId itemId))
                    {
                        ctx.Logger.LogWarning(
                            $"Invalid material input item id: {entry.itemId} on {def.materialId}");
                        continue;
                    }

                    ResourceId leftoverId = default;

                    if (!string.IsNullOrEmpty(entry.leftoverItemId))
                    {
                        if (!ResourceId.TryParse(entry.leftoverItemId, out leftoverId))
                        {
                            ctx.Logger.LogWarning(
                                $"Invalid leftover item id: {entry.leftoverItemId} on {def.materialId}");
                            leftoverId = default;
                        }
                    }

                    int value = entry.value > 0 ? entry.value : 1;
                    int needed = entry.needed > 0 ? entry.needed : 1;

                    materialInputRegistry.Register(new MaterialInputData(
                        itemId, materialId, value, needed, leftoverId));
                }
            }

            ctx.MaterialInputRegistry = materialInputRegistry;
            ctx.Logger.LogInfo(
                $"MaterialInputRegistry: {materialInputRegistry.Count} item->material mappings.");
        }
    }
}
