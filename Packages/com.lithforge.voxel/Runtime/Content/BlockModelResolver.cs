using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Resolves block models by walking parent chains and merging texture variables.
    /// For each model, produces a ResolvedFaceTextures with concrete texture ResourceIds
    /// for all 6 faces by following the parent chain to a built-in terminal model.
    /// </summary>
    public sealed class BlockModelResolver
    {
        private readonly ILogger _logger;
        private const int _maxParentDepth = 8;

        public BlockModelResolver(ILogger logger)
        {
            _logger = logger;
        }

        public Dictionary<ResourceId, ResolvedFaceTextures> ResolveAll(
            Dictionary<ResourceId, BlockModel> rawModels)
        {
            Dictionary<ResourceId, ResolvedFaceTextures> result =
                new Dictionary<ResourceId, ResolvedFaceTextures>();

            foreach (KeyValuePair<ResourceId, BlockModel> kvp in rawModels)
            {
                ResolvedFaceTextures resolved = Resolve(kvp.Key, rawModels);
                result[kvp.Key] = resolved;
            }

            _logger.LogInfo($"Resolved {result.Count} block models.");

            return result;
        }

        private ResolvedFaceTextures Resolve(
            ResourceId modelId,
            Dictionary<ResourceId, BlockModel> modelMap)
        {
            // Collect the full parent chain (leaf to root)
            List<BlockModel> chain = new List<BlockModel>();
            string terminalParent = null;

            ResourceId currentId = modelId;
            for (int depth = 0; depth <= _maxParentDepth; depth++)
            {

                if (!modelMap.TryGetValue(currentId, out BlockModel model))
                {
                    _logger.LogWarning($"Model '{currentId}' not found in model map.");

                    break;
                }

                chain.Add(model);

                if (string.IsNullOrEmpty(model.Parent))
                {
                    break;
                }

                if (BuiltInModelLibrary.IsBuiltIn(model.Parent))
                {
                    terminalParent = model.Parent;

                    break;
                }

                if (!ResourceId.TryParse(model.Parent, out ResourceId parentId))
                {
                    _logger.LogError($"Model '{currentId}' has invalid parent '{model.Parent}'.");

                    break;
                }

                currentId = parentId;
            }

            // Merge textures from root to leaf (parent first, child overrides)
            Dictionary<string, ResourceId> mergedTextures = new Dictionary<string, ResourceId>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                MergeModelTextures(chain[i], mergedTextures);
            }

            // Resolve with the terminal built-in parent
            if (terminalParent != null)
            {
                return BuiltInModelLibrary.Resolve(terminalParent, mergedTextures);
            }

            // No built-in parent found — use cube_all fallback
            _logger.LogWarning($"Model '{modelId}' has no built-in parent. Using cube_all fallback.");

            return BuiltInModelLibrary.Resolve("lithforge:block/cube_all", mergedTextures);
        }

        private void MergeModelTextures(
            BlockModel model,
            Dictionary<string, ResourceId> mergedTextures)
        {
            foreach (KeyValuePair<string, string> kvp in model.Textures)
            {
                string value = kvp.Value;

                if (value.StartsWith("#"))
                {
                    // Variable reference — look up in already-merged textures
                    string varName = value[1..];

                    if (mergedTextures.TryGetValue(varName, out ResourceId resolved))
                    {
                        mergedTextures[kvp.Key] = resolved;
                    }
                }
                else
                {
                    // Direct texture ResourceId
                    if (ResourceId.TryParse(value, out ResourceId texId))
                    {
                        mergedTextures[kvp.Key] = texId;
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid texture ResourceId '{value}' in model.");
                    }
                }
            }
        }
    }
}
