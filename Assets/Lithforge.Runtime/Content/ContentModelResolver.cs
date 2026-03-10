using System.Collections.Generic;
using Lithforge.Core.Data;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    /// <summary>
    /// Walks BlockModel.Parent chains via direct references,
    /// merges textures/elements, resolves #variable chains, and
    /// detects circular parents via HashSet.
    /// </summary>
    public sealed class ContentModelResolver
    {
        private const int _maxParentDepth = 16;

        public ResolvedFaceTextures Resolve(BlockModel model)
        {
            if (model == null)
            {
                return CreateMissing();
            }

            List<BlockModel> chain = new List<BlockModel>();
            HashSet<BlockModel> visited = new HashSet<BlockModel>();
            BuiltInParentType terminalType = BuiltInParentType.None;

            BlockModel current = model;

            for (int depth = 0; depth <= _maxParentDepth; depth++)
            {
                if (current == null)
                {
                    break;
                }

                if (!visited.Add(current))
                {
                    Debug.LogWarning($"[ContentModelResolver] Circular parent chain detected at '{current.name}'.");
                    break;
                }

                chain.Add(current);

                if (current.BuiltInParent != BuiltInParentType.None)
                {
                    terminalType = current.BuiltInParent;
                    break;
                }

                current = current.Parent;
            }

            // Merge textures from root to leaf (parent first, child overrides)
            Dictionary<string, string> mergedTextures = new Dictionary<string, string>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                IReadOnlyList<TextureVariable> textures = chain[i].Textures;

                for (int t = 0; t < textures.Count; t++)
                {
                    mergedTextures[textures[t].Variable] = textures[t].Value;
                }
            }

            // Resolve #variable references
            Dictionary<string, ResourceId> resolvedTextures = new Dictionary<string, ResourceId>();

            foreach (KeyValuePair<string, string> kvp in mergedTextures)
            {
                ResourceId resolved = ResolveTextureValue(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            return ResolveWithBuiltIn(terminalType, resolvedTextures);
        }

        private static ResourceId ResolveTextureValue(
            string value,
            Dictionary<string, string> mergedTextures)
        {
            HashSet<string> visitedVars = new HashSet<string>();
            string current = value;

            while (current != null && current.StartsWith("#"))
            {
                string varName = current.Substring(1);

                if (!visitedVars.Add(varName))
                {
                    Debug.LogWarning($"[ContentModelResolver] Circular texture variable reference: #{varName}");
                    break;
                }

                if (mergedTextures.TryGetValue(varName, out string resolved))
                {
                    current = resolved;
                }
                else
                {
                    break;
                }
            }

            if (current != null && !current.StartsWith("#") && ResourceId.TryParse(current, out ResourceId texId))
            {
                return texId;
            }

            return new ResourceId("lithforge", "block/missing");
        }

        private static ResolvedFaceTextures ResolveWithBuiltIn(
            BuiltInParentType parentType,
            Dictionary<string, ResourceId> textures)
        {
            switch (parentType)
            {
                case BuiltInParentType.CubeAll:
                    return ResolveCubeAll(textures);
                case BuiltInParentType.CubeColumn:
                    return ResolveCubeColumn(textures);
                case BuiltInParentType.Cube:
                    return ResolveCube(textures);
                default:
                    return ResolveCubeAll(textures);
            }
        }

        private static ResolvedFaceTextures ResolveCubeAll(Dictionary<string, ResourceId> textures)
        {
            if (textures.TryGetValue("all", out ResourceId allTex))
            {
                return new ResolvedFaceTextures
                {
                    North = allTex,
                    South = allTex,
                    East = allTex,
                    West = allTex,
                    Up = allTex,
                    Down = allTex,
                };
            }

            return ResolveCube(textures);
        }

        private static ResolvedFaceTextures ResolveCubeColumn(Dictionary<string, ResourceId> textures)
        {
            if (textures.TryGetValue("end", out ResourceId endTex) &&
                textures.TryGetValue("side", out ResourceId sideTex))
            {
                return new ResolvedFaceTextures
                {
                    North = sideTex,
                    South = sideTex,
                    East = sideTex,
                    West = sideTex,
                    Up = endTex,
                    Down = endTex,
                };
            }

            return ResolveCube(textures);
        }

        private static ResolvedFaceTextures ResolveCube(Dictionary<string, ResourceId> textures)
        {
            ResourceId missing = new ResourceId("lithforge", "block/missing");

            return new ResolvedFaceTextures
            {
                North = GetWithFallbacks(textures, missing, "north", "front", "side"),
                South = GetWithFallbacks(textures, missing, "south", "side"),
                East = GetWithFallbacks(textures, missing, "east", "side"),
                West = GetWithFallbacks(textures, missing, "west", "side"),
                Up = GetWithFallbacks(textures, missing, "up", "top", "end"),
                Down = GetWithFallbacks(textures, missing, "down", "bottom", "end"),
            };
        }

        private static ResourceId GetWithFallbacks(
            Dictionary<string, ResourceId> textures,
            ResourceId fallback,
            params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (textures.TryGetValue(keys[i], out ResourceId result))
                {
                    return result;
                }
            }

            return fallback;
        }

        private static ResolvedFaceTextures CreateMissing()
        {
            ResourceId missing = new ResourceId("lithforge", "block/missing");

            return new ResolvedFaceTextures
            {
                North = missing,
                South = missing,
                East = missing,
                West = missing,
                Up = missing,
                Down = missing,
            };
        }
    }
}
