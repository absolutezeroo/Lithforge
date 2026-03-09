using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Provides texture variable → face mappings for built-in parent model types.
    /// These are terminal nodes in the parent chain resolution.
    ///
    /// Built-in models:
    ///   lithforge:block/cube        — 6 independent face textures with alias fallbacks
    ///   lithforge:block/cube_all    — "all" maps to all 6 faces; falls through to cube if missing
    ///   lithforge:block/cube_column — "end" maps to up/down, "side" to 4 sides; falls through to cube
    ///
    /// Alias fallbacks in ResolveCube (Minecraft convention):
    ///   north ← front ← side       south ← side
    ///   east  ← side                west  ← side
    ///   up    ← top ← end          down  ← bottom ← end
    /// </summary>
    public static class BuiltInModelLibrary
    {
        private static readonly ResourceId _cubeId = new ResourceId("lithforge", "block/cube");
        private static readonly ResourceId _cubeAllId = new ResourceId("lithforge", "block/cube_all");
        private static readonly ResourceId _cubeColumnId = new ResourceId("lithforge", "block/cube_column");
        private static readonly ResourceId _missingTexture = new ResourceId("lithforge", "block/missing");

        public static bool IsBuiltIn(string parentRef)
        {
            if (string.IsNullOrEmpty(parentRef))
            {
                return false;
            }

            if (!ResourceId.TryParse(parentRef, out ResourceId parentId))
            {
                return false;
            }

            return parentId.Equals(_cubeId)
                || parentId.Equals(_cubeAllId)
                || parentId.Equals(_cubeColumnId);
        }

        public static ResolvedFaceTextures Resolve(
            string parentRef,
            Dictionary<string, ResourceId> mergedTextures)
        {
            ResourceId parentId = ResourceId.Parse(parentRef);

            if (parentId.Equals(_cubeAllId))
            {
                return ResolveCubeAll(mergedTextures);
            }

            if (parentId.Equals(_cubeColumnId))
            {
                return ResolveCubeColumn(mergedTextures);
            }

            if (parentId.Equals(_cubeId))
            {
                return ResolveCube(mergedTextures);
            }

            return CreateMissing();
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

            // "all" not found — fall through to cube with alias resolution
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

            // Missing expected keys — fall through to cube with alias resolution
            return ResolveCube(textures);
        }

        private static ResolvedFaceTextures ResolveCube(Dictionary<string, ResourceId> textures)
        {
            return new ResolvedFaceTextures
            {
                North = GetTextureWithFallbacks(textures, "north", "front", "side"),
                South = GetTextureWithFallbacks(textures, "south", "side"),
                East = GetTextureWithFallbacks(textures, "east", "side"),
                West = GetTextureWithFallbacks(textures, "west", "side"),
                Up = GetTextureWithFallbacks(textures, "up", "top", "end"),
                Down = GetTextureWithFallbacks(textures, "down", "bottom", "end"),
            };
        }

        private static ResourceId GetTextureWithFallbacks(
            Dictionary<string, ResourceId> textures, string primary, string fallback1)
        {
            if (textures.TryGetValue(primary, out ResourceId result))
            {
                return result;
            }

            if (textures.TryGetValue(fallback1, out result))
            {
                return result;
            }

            return _missingTexture;
        }

        private static ResourceId GetTextureWithFallbacks(
            Dictionary<string, ResourceId> textures, string primary, string fallback1, string fallback2)
        {
            if (textures.TryGetValue(primary, out ResourceId result))
            {
                return result;
            }

            if (textures.TryGetValue(fallback1, out result))
            {
                return result;
            }

            if (textures.TryGetValue(fallback2, out result))
            {
                return result;
            }

            return _missingTexture;
        }

        private static ResolvedFaceTextures CreateMissing()
        {
            return new ResolvedFaceTextures
            {
                North = _missingTexture,
                South = _missingTexture,
                East = _missingTexture,
                West = _missingTexture,
                Up = _missingTexture,
                Down = _missingTexture,
            };
        }
    }
}
