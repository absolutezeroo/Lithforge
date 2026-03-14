using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Walks BlockModel.Parent chains via direct references,
    /// merges textures/elements, resolves #variable chains, and
    /// detects circular parents via HashSet.
    /// </summary>
    public sealed class ContentModelResolver
    {
        private const int MaxParentDepth = 16;
        private readonly Dictionary<BlockModel, ResolvedFaceTextures2D> _resolvedCache =
            new Dictionary<BlockModel, ResolvedFaceTextures2D>();

        public ResolvedFaceTextures2D Resolve(BlockModel model)
        {
            if (model == null)
            {
                return CreateMissing();
            }

            if (_resolvedCache.TryGetValue(model, out ResolvedFaceTextures2D cached))
            {
                return cached;
            }

            List<BlockModel> chain = new List<BlockModel>();
            HashSet<BlockModel> visited = new HashSet<BlockModel>();
            BuiltInParentType terminalType = BuiltInParentType.None;

            BlockModel current = model;

            for (int depth = 0; depth <= MaxParentDepth; depth++)
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
            Dictionary<string, TextureVariable> mergedTextures = new Dictionary<string, TextureVariable>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                IReadOnlyList<TextureVariable> textures = chain[i].Textures;

                for (int t = 0; t < textures.Count; t++)
                {
                    mergedTextures[textures[t].Variable] = textures[t];
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

            // Resolve #variable references to Texture2D
            Dictionary<string, Texture2D> resolvedTextures = new Dictionary<string, Texture2D>();

            foreach (KeyValuePair<string, TextureVariable> kvp in mergedTextures)
            {
                Texture2D resolved = ResolveTextureVariable(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            ResolvedModel result = new ResolvedModel();
            result.Textures = ResolveWithBuiltIn(terminalType, resolvedTextures);
            result.Elements = mergedElements;

            _resolvedCache[model] = result.Textures;

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

            for (int depth = 0; depth <= MaxParentDepth; depth++)
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

            Dictionary<string, TextureVariable> mergedTextures = new Dictionary<string, TextureVariable>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                IReadOnlyList<TextureVariable> textures = chain[i].Textures;

                for (int t = 0; t < textures.Count; t++)
                {
                    mergedTextures[textures[t].Variable] = textures[t];
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

            Dictionary<string, Texture2D> resolvedTextures = new Dictionary<string, Texture2D>();

            foreach (KeyValuePair<string, TextureVariable> kvp in mergedTextures)
            {
                Texture2D resolved = ResolveTextureVariable(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            return new ResolvedModel
            {
                Textures = ResolveWithBuiltIn(terminalType, resolvedTextures),
                Elements = mergedElements,
                ResolvedTextureDictionary = resolvedTextures,
            };
        }

        private static Texture2D ResolveTextureVariable(
            TextureVariable texVar,
            Dictionary<string, TextureVariable> mergedTextures)
        {
            // If it has a direct Texture2D, use it
            if (!texVar.IsVariableReference)
            {
                return texVar.Texture;
            }

            // Walk #variable chain
            HashSet<string> visitedVars = new HashSet<string>();
            string currentRef = texVar.VariableReference;

            while (currentRef != null && currentRef.StartsWith("#"))
            {
                string varName = currentRef.Substring(1);

                if (!visitedVars.Add(varName))
                {
                    UnityEngine.Debug.LogWarning($"[ContentModelResolver] Circular texture variable reference: #{varName}");
                    break;
                }

                if (mergedTextures.TryGetValue(varName, out TextureVariable resolved))
                {
                    if (!resolved.IsVariableReference)
                    {
                        return resolved.Texture;
                    }

                    currentRef = resolved.VariableReference;
                }
                else
                {
                    break;
                }
            }

            // Could not resolve — return null (handled downstream as missing)
            return null;
        }

        private static ResolvedFaceTextures2D ResolveWithBuiltIn(
            BuiltInParentType parentType,
            Dictionary<string, Texture2D> textures)
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

        private static ResolvedFaceTextures2D ResolveCubeAll(Dictionary<string, Texture2D> textures)
        {
            if (textures.TryGetValue("all", out Texture2D allTex))
            {
                return new ResolvedFaceTextures2D
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

        private static ResolvedFaceTextures2D ResolveCubeColumn(Dictionary<string, Texture2D> textures)
        {
            if (textures.TryGetValue("end", out Texture2D endTex) &&
                textures.TryGetValue("side", out Texture2D sideTex))
            {
                return new ResolvedFaceTextures2D
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

        private static ResolvedFaceTextures2D ResolveCube(Dictionary<string, Texture2D> textures)
        {
            return new ResolvedFaceTextures2D
            {
                North = GetWithFallbacks(textures, null, "north", "front", "side"),
                South = GetWithFallbacks(textures, null, "south", "side"),
                East = GetWithFallbacks(textures, null, "east", "side"),
                West = GetWithFallbacks(textures, null, "west", "side"),
                Up = GetWithFallbacks(textures, null, "up", "top", "end"),
                Down = GetWithFallbacks(textures, null, "down", "bottom", "end"),
            };
        }

        private static ResolvedFaceTextures2D ResolveCubeBottomTop(Dictionary<string, Texture2D> textures)
        {
            Texture2D top = GetWithFallbacks(textures, null, "top", "end");
            Texture2D bottom = GetWithFallbacks(textures, null, "bottom", "end");
            Texture2D side = GetWithFallbacks(textures, null, "side");

            return new ResolvedFaceTextures2D
            {
                North = side,
                South = side,
                East = side,
                West = side,
                Up = top,
                Down = bottom,
            };
        }

        private static ResolvedFaceTextures2D ResolveOrientable(Dictionary<string, Texture2D> textures)
        {
            return new ResolvedFaceTextures2D
            {
                North = GetWithFallbacks(textures, null, "front", "north", "side"),
                South = GetWithFallbacks(textures, null, "south", "side"),
                East = GetWithFallbacks(textures, null, "east", "side"),
                West = GetWithFallbacks(textures, null, "west", "side"),
                Up = GetWithFallbacks(textures, null, "top", "up", "end"),
                Down = GetWithFallbacks(textures, null, "bottom", "down", "end"),
            };
        }

        private static ResolvedFaceTextures2D ResolveCross(Dictionary<string, Texture2D> textures)
        {
            Texture2D cross = GetWithFallbacks(textures, null, "cross", "all");

            return new ResolvedFaceTextures2D
            {
                North = cross,
                South = cross,
                East = cross,
                West = cross,
                Up = cross,
                Down = cross,
            };
        }

        private static Texture2D GetWithFallbacks(
            Dictionary<string, Texture2D> textures,
            Texture2D fallback,
            params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (textures.TryGetValue(keys[i], out Texture2D result))
                {
                    return result;
                }
            }

            return fallback;
        }

        private static ResolvedFaceTextures2D CreateMissing()
        {
            return new ResolvedFaceTextures2D
            {
                North = null,
                South = null,
                East = null,
                West = null,
                Up = null,
                Down = null,
            };
        }
    }
}
