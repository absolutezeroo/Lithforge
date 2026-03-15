namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// Rendering pass for a block, mapped to submesh indices in <see cref="Lithforge.Runtime.Rendering.MegaMeshBuffer"/>.
    /// Controls transparency sorting and shader selection.
    /// </summary>
    public enum RenderLayerType
    {
        /// <summary>Fully solid — submesh 0, no alpha. Majority of blocks.</summary>
        Opaque = 0,
        /// <summary>Binary alpha test (leaves, flowers, tall grass) — submesh 1, Cull Off.</summary>
        Cutout = 1,
        /// <summary>Alpha-blended (water, glass, ice) — submesh 2, sorted back-to-front.</summary>
        Translucent = 2,
    }
}
