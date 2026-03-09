using Lithforge.Core.Registry;

namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Type alias for the block definition registry.
    /// Wraps Registry&lt;BlockDefinition&gt; for clarity.
    /// </summary>
    public sealed class BlockRegistry
    {
        private readonly Registry<BlockDefinition> _inner;

        public BlockRegistry(Registry<BlockDefinition> inner)
        {
            _inner = inner;
        }

        public Registry<BlockDefinition> Inner
        {
            get { return _inner; }
        }
    }
}
