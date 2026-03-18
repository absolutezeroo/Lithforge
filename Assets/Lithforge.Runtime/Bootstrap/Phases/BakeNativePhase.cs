using System.Collections.Generic;

using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;

using Unity.Collections;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class BakeNativePhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Baking native data...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            ctx.NativeStateRegistry = ctx.StateRegistry.BakeNative(Allocator.Persistent);
            ctx.NativeAtlasLookup = BakeAtlasLookup(
                ctx.StateRegistry, ctx.AtlasResult, ctx.ResolvedFaces);
        }

        internal static NativeAtlasLookup BakeAtlasLookup(
            StateRegistry stateRegistry,
            AtlasResult atlasResult,
            Dictionary<StateId, ResolvedFaceTextures2D> resolvedFaces)
        {
            int totalStates = stateRegistry.TotalStateCount;
            NativeArray<AtlasEntry> entries = new(
                totalStates, Allocator.Persistent);

            for (int i = 0; i < totalStates; i++)
            {
                StateId sid = new((ushort)i);
                BlockStateCompact state = stateRegistry.GetState(sid);

                AtlasEntry entry = new()
                {
                    // Base texture indices (from StateRegistry, already patched)
                    TexPosX = state.TexEast,
                    TexNegX = state.TexWest,
                    TexPosY = state.TexUp,
                    TexNegY = state.TexDown,
                    TexPosZ = state.TexSouth,
                    TexNegZ = state.TexNorth,

                    // Defaults: no overlay
                    OvlPosX = 0xFFFF,
                    OvlNegX = 0xFFFF,
                    OvlPosY = 0xFFFF,
                    OvlNegY = 0xFFFF,
                    OvlPosZ = 0xFFFF,
                    OvlNegZ = 0xFFFF,
                    BaseTintPacked = 0,
                    OverlayTintPacked = 0,
                };

                // Populate overlay + per-face tint from resolved face data
                if (resolvedFaces.TryGetValue(sid, out ResolvedFaceTextures2D faces))
                {
                    // Per-face base tint (face direction: PosX=East, NegX=West, PosY=Up, NegY=Down, PosZ=South, NegZ=North)
                    entry.BaseTintPacked = PackFaceTints(
                        faces.TintEast, faces.TintWest,
                        faces.TintUp, faces.TintDown,
                        faces.TintSouth, faces.TintNorth);

                    // Overlay textures
                    entry.OvlPosX = GetOverlayIndex(atlasResult, faces.OverlayEast);
                    entry.OvlNegX = GetOverlayIndex(atlasResult, faces.OverlayWest);
                    entry.OvlPosY = GetOverlayIndex(atlasResult, faces.OverlayUp);
                    entry.OvlNegY = GetOverlayIndex(atlasResult, faces.OverlayDown);
                    entry.OvlPosZ = GetOverlayIndex(atlasResult, faces.OverlaySouth);
                    entry.OvlNegZ = GetOverlayIndex(atlasResult, faces.OverlayNorth);

                    // Per-face overlay tint
                    entry.OverlayTintPacked = PackFaceTints(
                        faces.OverlayTintEast, faces.OverlayTintWest,
                        faces.OverlayTintUp, faces.OverlayTintDown,
                        faces.OverlayTintSouth, faces.OverlayTintNorth);
                }

                entries[i] = entry;
            }

            int textureCount = 0;

            if (atlasResult.TextureArray != null)
            {
                textureCount = atlasResult.TextureArray.depth;
            }

            return new NativeAtlasLookup(entries, textureCount);
        }

        private static ushort PackFaceTints(
            byte posX, byte negX, byte posY, byte negY, byte posZ, byte negZ)
        {
            return (ushort)(
                posX & 0x3 |
                (negX & 0x3) << 2 |
                (posY & 0x3) << 4 |
                (negY & 0x3) << 6 |
                (posZ & 0x3) << 8 |
                (negZ & 0x3) << 10);
        }

        private static ushort GetOverlayIndex(AtlasResult atlas, Texture2D texture)
        {
            if (texture != null && atlas.IndexByTexture.TryGetValue(texture, out int index))
            {
                return (ushort)index;
            }

            return 0xFFFF;
        }
    }
}
