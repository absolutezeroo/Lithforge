namespace Lithforge.Core.Data
{
    /// <summary>
    /// Fully resolved per-face texture ResourceIds for a single block state.
    /// Produced by the model resolution chain: blockstate -> model -> parent -> concrete textures.
    /// </summary>
    public struct ResolvedFaceTextures
    {
        public ResourceId North;
        public ResourceId South;
        public ResourceId East;
        public ResourceId West;
        public ResourceId Up;
        public ResourceId Down;
    }
}
