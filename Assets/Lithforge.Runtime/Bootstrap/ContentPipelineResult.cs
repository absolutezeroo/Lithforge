using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering.Atlas;
using Lithforge.Voxel.Block;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    /// Result of the content pipeline build.
    /// Contains all registries and atlas data needed for runtime operation.
    /// </summary>
    public sealed class ContentPipelineResult
    {
        public StateRegistry StateRegistry { get; }
        public NativeStateRegistry NativeStateRegistry { get; }
        public NativeAtlasLookup NativeAtlasLookup { get; }
        public AtlasResult AtlasResult { get; }

        public ContentPipelineResult(
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            AtlasResult atlasResult)
        {
            StateRegistry = stateRegistry;
            NativeStateRegistry = nativeStateRegistry;
            NativeAtlasLookup = nativeAtlasLookup;
            AtlasResult = atlasResult;
        }
    }
}
