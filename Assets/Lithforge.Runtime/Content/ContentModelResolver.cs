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
                    UnityEngine.Debug.LogWarning($"[ContentModelResolver] Circular parent chain detected at '{current.name}'.");
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

            // Merge elements: child elements override parent entirely if present
            List<ModelElement> mergedElements = new List<ModelElement>();

            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i].Elements.Count > 0)
                {
                    mergedElements.Clear();

                    for (int e = 0; e < chain[i].Elements.Count; e++)
                    {
                        mergedElements.Add(chain[i].Elements[e]);
                    }
                }
            }

            // Resolve #variable references
            Dictionary<string, ResourceId> resolvedTextures = new Dictionary<string, ResourceId>();

            foreach (KeyValuePair<string, string> kvp in mergedTextures)
            {
                ResourceId resolved = ResolveTextureValue(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            ResolvedModel result = new ResolvedModel();
            result.Textures = ResolveWithBuiltIn(terminalType, resolvedTextures);
            result.Elements = mergedElements;

            return result.Textures;
        }

        /// <summary>
        /// Resolves a block model to both face textures and merged element list.
        /// </summary>
        public ResolvedModel ResolveFull(BlockModel model)
        {
            if (model == null)
            {
                return new ResolvedModel
                {
                    Textures = CreateMissing(),
                    Elements = new List<ModelElement>(),
                };
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
                    UnityEngine.Debug.LogWarning($"[ContentModelResolver] Circular parent chain detected at '{current.name}'.");
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

            Dictionary<string, string> mergedTextures = new Dictionary<string, string>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                IReadOnlyList<TextureVariable> textures = chain[i].Textures;

                for (int t = 0; t < textures.Count; t++)
                {
                    mergedTextures[textures[t].Variable] = textures[t].Value;
                }
            }

            List<ModelElement> mergedElements = new List<ModelElement>();

            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i].Elements.Count > 0)
                {
                    mergedElements.Clear();

                    for (int e = 0; e < chain[i].Elements.Count; e++)
                    {
                        mergedElements.Add(chain[i].Elements[e]);
                    }
                }
            }

            Dictionary<string, ResourceId> resolvedTextures = new Dictionary<string, ResourceId>();

            foreach (KeyValuePair<string, string> kvp in mergedTextures)
            {
                ResourceId resolved = ResolveTextureValue(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            return new ResolvedModel
            {
                Textures = ResolveWithBuiltIn(terminalType, resolvedTextures),
                Elements = mergedElements,
            };
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
                    UnityEngine.Debug.LogWarning($"[ContentModelResolver] Circular texture variable reference: #{varName}");
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
                case BuiltInParentType.CubeBottomTop:
                    return ResolveCubeBottomTop(textures);
                case BuiltInParentType.Orientable:
                    return ResolveOrientable(textures);
                case BuiltInParentType.Cross:
                    return ResolveCross(textures);
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

        private static ResolvedFaceTextures ResolveCubeBottomTop(Dictionary<string, ResourceId> textures)
        {
            ResourceId missing = new ResourceId("lithforge", "block/missing");
            ResourceId top = GetWithFallbacks(textures, missing, "top", "end");
            ResourceId bottom = GetWithFallbacks(textures, missing, "bottom", "end");
            ResourceId side = GetWithFallbacks(textures, missing, "side");

            return new ResolvedFaceTextures
            {
                North = side,
                South = side,
                East = side,
                West = side,
                Up = top,
                Down = bottom,
            };
        }

        private static ResolvedFaceTextures ResolveOrientable(Dictionary<string, ResourceId> textures)
        {
            ResourceId missing = new ResourceId("lithforge", "block/missing");

            return new ResolvedFaceTextures
            {
                North = GetWithFallbacks(textures, missing, "front", "north", "side"),
                South = GetWithFallbacks(textures, missing, "south", "side"),
                East = GetWithFallbacks(textures, missing, "east", "side"),
                West = GetWithFallbacks(textures, missing, "west", "side"),
                Up = GetWithFallbacks(textures, missing, "top", "up", "end"),
                Down = GetWithFallbacks(textures, missing, "bottom", "down", "end"),
            };
        }

        private static ResolvedFaceTextures ResolveCross(Dictionary<string, ResourceId> textures)
        {
            ResourceId missing = new ResourceId("lithforge", "block/missing");
            ResourceId cross = GetWithFallbacks(textures, missing, "cross", "all");

            return new ResolvedFaceTextures
            {
                North = cross,
                South = cross,
                East = cross,
                West = cross,
                Up = cross,
                Down = cross,
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
