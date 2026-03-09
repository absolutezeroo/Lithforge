using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Content
{
    /// <summary>
    /// Provides texture variable → face mappings for built-in parent model types.
    /// These are terminal nodes in the parent chain resolution.
    ///
    /// Built-in models:
    ///   lithforge:block/cube     — 6 independent face textures (north/south/east/west/up/down)
    ///   lithforge:block/cube_all — "all" maps to all 6 faces
    ///   lithforge:block/cube_column — "end" maps to up/down, "side" maps to 4 sides
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
            ResourceId allTex = GetTexture(textures, "all");

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

        private static ResolvedFaceTextures ResolveCubeColumn(Dictionary<string, ResourceId> textures)
        {
            ResourceId endTex = GetTexture(textures, "end");
            ResourceId sideTex = GetTexture(textures, "side");

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

        private static ResolvedFaceTextures ResolveCube(Dictionary<string, ResourceId> textures)
        {
            return new ResolvedFaceTextures
            {
                North = GetTexture(textures, "north"),
                South = GetTexture(textures, "south"),
                East = GetTexture(textures, "east"),
                West = GetTexture(textures, "west"),
                Up = GetTexture(textures, "up"),
                Down = GetTexture(textures, "down"),
            };
        }

        private static ResourceId GetTexture(Dictionary<string, ResourceId> textures, string key)
        {

            if (textures.TryGetValue(key, out ResourceId result))
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
