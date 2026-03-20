namespace Lithforge.Voxel.BlockEntity
{
    /// <summary>
    ///     Callback surface provided to block entities so they can signal persistent
    ///     state changes to the owning chunk. Marks the chunk dirty for save-on-unload.
    /// </summary>
    public interface IBlockEntityHost
    {
        /// <summary>Marks the owning chunk dirty because block entity state has changed.</summary>
        public void NotifyDirty();
    }
}
