namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Crafting table block entity. Has no persistent state — serves as a marker
    /// so BlockInteraction can detect right-click and open CraftingTableScreen.
    /// The 3x3 crafting grid lives in the screen, not the entity.
    /// </summary>
    public sealed class CraftingTableBlockEntity : BlockEntity
    {
        public const string TypeIdValue = "lithforge:crafting_table";

        public override string TypeId
        {
            get { return TypeIdValue; }
        }

        public CraftingTableBlockEntity()
        {
            Behaviors = System.Array.Empty<BlockEntityBehavior>();
        }
    }
}
