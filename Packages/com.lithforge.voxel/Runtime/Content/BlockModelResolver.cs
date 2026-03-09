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
                ResolvedFaceTextures resolved = Resolve(kvp.Key, rawModels, 0);
                result[kvp.Key] = resolved;
            }

            _logger.LogInfo($"Resolved {result.Count} block models.");
            return result;
        }

        private ResolvedFaceTextures Resolve(
            ResourceId modelId,
            Dictionary<ResourceId, BlockModel> modelMap,
            int depth)
        {
            if (depth > _maxParentDepth)
            {
                _logger.LogError($"Model parent chain too deep for '{modelId}' (max {_maxParentDepth}).");
                return new ResolvedFaceTextures();
            }

            BlockModel model;

            if (!modelMap.TryGetValue(modelId, out model))
            {
                _logger.LogWarning($"Model '{modelId}' not found in model map.");
                return new ResolvedFaceTextures();
            }

            // Merge textures from this model — resolve #variable references
            Dictionary<string, ResourceId> mergedTextures = new Dictionary<string, ResourceId>();
            ResolveTextureVariables(model, modelMap, mergedTextures, depth);

            // Check if the parent is a built-in terminal
            if (BuiltInModelLibrary.IsBuiltIn(model.Parent))
            {
                return BuiltInModelLibrary.Resolve(model.Parent, mergedTextures);
            }

            // No parent — treat this model as a cube_all fallback
            if (string.IsNullOrEmpty(model.Parent))
            {
                _logger.LogWarning($"Model '{modelId}' has no parent. Using cube_all fallback.");
                return BuiltInModelLibrary.Resolve("lithforge:block/cube_all", mergedTextures);
            }

            // Walk up to file-based parent
            ResourceId parentId;

            if (!ResourceId.TryParse(model.Parent, out parentId))
            {
                _logger.LogError($"Model '{modelId}' has invalid parent '{model.Parent}'.");
                return new ResolvedFaceTextures();
            }

            return Resolve(parentId, modelMap, depth + 1);
        }

        private void ResolveTextureVariables(
            BlockModel model,
            Dictionary<ResourceId, BlockModel> modelMap,
            Dictionary<string, ResourceId> mergedTextures,
            int depth)
        {
            // First, collect textures from parent chain (recursively)
            if (!string.IsNullOrEmpty(model.Parent) && !BuiltInModelLibrary.IsBuiltIn(model.Parent))
            {
                ResourceId parentId;

                if (ResourceId.TryParse(model.Parent, out parentId))
                {
                    BlockModel parentModel;

                    if (modelMap.TryGetValue(parentId, out parentModel) && depth < _maxParentDepth)
                    {
                        ResolveTextureVariables(parentModel, modelMap, mergedTextures, depth + 1);
                    }
                }
            }

            // Then, overlay this model's textures (child overrides parent)
            foreach (KeyValuePair<string, string> kvp in model.Textures)
            {
                string value = kvp.Value;

                if (value.StartsWith("#"))
                {
                    // Variable reference — look up in already-merged textures
                    string varName = value.Substring(1);
                    ResourceId resolved;

                    if (mergedTextures.TryGetValue(varName, out resolved))
                    {
                        mergedTextures[kvp.Key] = resolved;
                    }
                }
                else
                {
                    // Direct texture ResourceId
                    ResourceId texId;

                    if (ResourceId.TryParse(value, out texId))
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
