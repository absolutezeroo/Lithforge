namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Crafting table block entity. Has no persistent state — serves as a marker
    /// so BlockInteraction can detect right-click and open CraftingTableScreen.
    /// The 3x3 crafting grid lives in the screen, not the entity.
    /// </summary>
    public sealed class CraftingTableBlockEntity : BlockEntity
    {
        /// <summary>Unique type identifier for crafting table block entities.</summary>
        public const string TypeIdValue = "lithforge:crafting_table";

        /// <summary>Returns the crafting table type identifier.</summary>
        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        /// <summary>Creates a crafting table entity with no behaviors (marker only).</summary>
        public CraftingTableBlockEntity()
        {
            Behaviors = System.Array.Empty<BlockEntityBehavior>();
        }
    }
}
