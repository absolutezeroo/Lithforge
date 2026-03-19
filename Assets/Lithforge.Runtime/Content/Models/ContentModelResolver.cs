using System.Collections.Generic;

using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    ///     Walks BlockModel.Parent chains via direct references,
    ///     merges textures/elements, resolves #variable chains, and
    ///     detects circular parents via HashSet.
    /// </summary>
    public sealed class ContentModelResolver
    {
        /// <summary>Maximum parent chain depth before aborting to prevent infinite loops.</summary>
        private const int MaxParentDepth = 16;

        /// <summary>Cache of already-resolved models to avoid redundant parent chain walks.</summary>
        private readonly Dictionary<BlockModel, ResolvedFaceTextures2D> _resolvedCache = new();

        /// <summary>Resolves a BlockModel to its per-face Texture2D references by walking the parent chain.</summary>
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

            List<BlockModel> chain = new();
            HashSet<BlockModel> visited = new();
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
            Dictionary<string, TextureVariable> mergedTextures = new();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                IReadOnlyList<TextureVariable> textures = chain[i].Textures;

                for (int t = 0; t < textures.Count; t++)
                {
                    mergedTextures[textures[t].Variable] = textures[t];
                }
            }

            // Merge elements: child elements override parent entirely if present
            List<ModelElement> mergedElements = new();

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
            Dictionary<string, Texture2D> resolvedTextures = new();

            foreach (KeyValuePair<string, TextureVariable> kvp in mergedTextures)
            {
                Texture2D resolved = ResolveTextureVariable(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            ResolvedModel result = new()
            {
                Textures = ResolveWithBuiltIn(terminalType, resolvedTextures), Elements = mergedElements,
            };

            _resolvedCache[model] = result.Textures;

            return result.Textures;
        }

        /// <summary>
        ///     Resolves a block model to both face textures and merged element list.
        /// </summary>
        public ResolvedModel ResolveFull(BlockModel model)
        {
            if (model == null)
            {
                return new ResolvedModel
                {
                    Textures = CreateMissing(), Elements = new List<ModelElement>(),
                };
            }

            List<BlockModel> chain = new();
            HashSet<BlockModel> visited = new();
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

            Dictionary<string, TextureVariable> mergedTextures = new();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                IReadOnlyList<TextureVariable> textures = chain[i].Textures;

                for (int t = 0; t < textures.Count; t++)
                {
                    mergedTextures[textures[t].Variable] = textures[t];
                }
            }

            List<ModelElement> mergedElements = new();

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

            Dictionary<string, Texture2D> resolvedTextures = new();

            foreach (KeyValuePair<string, TextureVariable> kvp in mergedTextures)
            {
                Texture2D resolved = ResolveTextureVariable(kvp.Value, mergedTextures);
                resolvedTextures[kvp.Key] = resolved;
            }

            return new ResolvedModel
            {
                Textures = ResolveWithBuiltIn(terminalType, resolvedTextures), Elements = mergedElements, ResolvedTextureDictionary = resolvedTextures,
            };
        }

        /// <summary>Follows #variable indirection chains to resolve a TextureVariable to a concrete Texture2D.</summary>
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
            HashSet<string> visitedVars = new();
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

        /// <summary>Dispatches to the appropriate built-in parent resolver based on the terminal parent type.</summary>
        private static ResolvedFaceTextures2D ResolveWithBuiltIn(
            BuiltInParentType parentType,
            Dictionary<string, Texture2D> textures)
        {
            return parentType switch
            {
                BuiltInParentType.CubeAll => ResolveCubeAll(textures),
                BuiltInParentType.CubeColumn => ResolveCubeColumn(textures),
                BuiltInParentType.Cube => ResolveCube(textures),
                BuiltInParentType.CubeBottomTop => ResolveCubeBottomTop(textures),
                BuiltInParentType.Orientable => ResolveOrientable(textures),
                BuiltInParentType.Cross => ResolveCross(textures),
                _ => ResolveCubeAll(textures),
            };
        }

        /// <summary>Resolves a CubeAll model: all six faces use the "all" variable.</summary>
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

        /// <summary>Resolves a CubeColumn model: top/bottom use "end", sides use "side".</summary>
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

        /// <summary>Resolves a Cube model: each face uses its own named variable with fallback chains.</summary>
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

        /// <summary>Resolves a CubeBottomTop model: distinct top/bottom, four sides use "side".</summary>
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

        /// <summary>Resolves an Orientable model: distinguished front face, with fallback chains for other faces.</summary>
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

        /// <summary>Resolves a Cross model: all faces use the "cross" variable with "all" fallback.</summary>
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

        /// <summary>Looks up a texture by trying keys in order, returning the fallback if none match.</summary>
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

        /// <summary>
        ///     Resolves the first-person right hand display transform by walking the parent chain.
        ///     Returns the first ModelDisplayTransform with HasValue=true (leaf wins over root).
        ///     Returns null if no display transform is found in the chain.
        /// </summary>
        public ModelDisplayTransform ResolveFirstPersonRightHand(BlockModel model)
        {
            if (model == null)
            {
                return null;
            }

            HashSet<BlockModel> visited = new();
            BlockModel current = model;

            for (int depth = 0; depth <= MaxParentDepth; depth++)
            {
                if (current == null)
                {
                    break;
                }

                if (!visited.Add(current))
                {
                    break;
                }

                if (current.FirstPersonRightHand is
                    {
                        HasValue: true,
                    })
                {
                    return current.FirstPersonRightHand;
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>Creates a ResolvedFaceTextures2D with all faces set to null (missing texture).</summary>
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
