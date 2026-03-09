using System.Collections.Generic;
using System.Text;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Block;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Resolves blockstate variant keys to per-face textures for every StateId.
    /// For each registered block, decodes the state offset back into property values,
    /// constructs a variant key, and looks up the corresponding model.
    /// </summary>
    public sealed class BlockstateResolver
    {
        private readonly ILogger _logger;

        public BlockstateResolver(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Resolves all registered states to per-face textures.
        /// </summary>
        public Dictionary<StateId, ResolvedFaceTextures> ResolveAll(
            IReadOnlyList<StateRegistryEntry> entries,
            Dictionary<ResourceId, BlockstateDefinition> blockstates,
            Dictionary<ResourceId, ResolvedFaceTextures> resolvedModels)
        {
            Dictionary<StateId, ResolvedFaceTextures> result =
                new Dictionary<StateId, ResolvedFaceTextures>();

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];
                BlockDefinition def = entry.Definition;

                if (!blockstates.TryGetValue(def.Id, out BlockstateDefinition blockstate))
                {
                    _logger.LogWarning($"No blockstate for '{def.Id}'. Using default textures.");
                    continue;
                }

                for (int offset = 0; offset < entry.StateCount; offset++)
                {
                    StateId stateId = new StateId((ushort)(entry.BaseStateId + offset));
                    string variantKey = BuildVariantKey(def, offset);

                    if (!blockstate.Variants.TryGetValue(variantKey, out BlockstateVariant variant))
                    {
                        _logger.LogWarning(
                            $"Blockstate '{def.Id}' has no variant for key '{variantKey}'.");
                        continue;
                    }

                    if (resolvedModels.TryGetValue(variant.Model, out ResolvedFaceTextures faces))
                    {
                        result[stateId] = faces;
                    }
                    else
                    {
                        _logger.LogWarning(
                            $"Model '{variant.Model}' not found for blockstate '{def.Id}' variant '{variantKey}'.");
                    }
                }
            }

            _logger.LogInfo($"Resolved {result.Count} block state face textures.");
            return result;
        }

        /// <summary>
        /// Decodes a state offset back into property values and builds the variant key string.
        /// Uses the same cartesian product encoding as StateRegistry.Register().
        /// </summary>
        private static string BuildVariantKey(BlockDefinition def, int stateOffset)
        {
            IReadOnlyList<PropertyDefinition> properties = def.Properties;

            if (properties == null || properties.Count == 0)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            int remaining = stateOffset;

            for (int i = 0; i < properties.Count; i++)
            {
                PropertyDefinition prop = properties[i];
                int valueIndex = remaining % prop.ValueCount;
                remaining /= prop.ValueCount;

                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(prop.Name);
                sb.Append('=');
                sb.Append(prop.Values[valueIndex]);
            }

            return sb.ToString();
        }
    }
}
