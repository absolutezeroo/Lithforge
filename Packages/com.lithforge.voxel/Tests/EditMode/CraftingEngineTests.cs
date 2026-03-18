using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Crafting;
using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class CraftingEngineTests
    {
        private static readonly ResourceId s_plank = new("lithforge", "oak_planks");
        private static readonly ResourceId s_stick = new("lithforge", "stick");
        private static readonly ResourceId s_craftingTable = new("lithforge", "crafting_table");
        private static readonly ResourceId s_stonePickaxe = new("lithforge", "stone_pickaxe");
        private static readonly ResourceId s_cobblestone = new("lithforge", "cobblestone");

        [Test]
        public void FindMatch_ShapedRecipe_MatchesPattern()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = s_craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', s_plank } },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new(3, 3);
            grid.SetSlot(0, 0, s_plank);
            grid.SetSlot(1, 0, s_plank);
            grid.SetSlot(0, 1, s_plank);
            grid.SetSlot(1, 1, s_plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(s_craftingTable, match.ResultItem);
        }

        [Test]
        public void FindMatch_ShapedRecipe_WrongPattern_ReturnsNull()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = s_craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', s_plank } },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new(3, 3);
            grid.SetSlot(0, 0, s_plank);
            grid.SetSlot(1, 0, s_plank);
            grid.SetSlot(2, 0, s_plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_ShapedRecipe_OffsetPosition_Matches()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = s_craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', s_plank } },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            // Place pattern offset to bottom-right corner
            CraftingGrid grid = new(3, 3);
            grid.SetSlot(1, 1, s_plank);
            grid.SetSlot(2, 1, s_plank);
            grid.SetSlot(1, 2, s_plank);
            grid.SetSlot(2, 2, s_plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(s_craftingTable, match.ResultItem);
        }

        [Test]
        public void FindMatch_ShapelessRecipe_AnyOrder_Matches()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "sticks_recipe"))
            {
                Type = RecipeType.Shapeless,
                ResultItem = s_stick,
                ResultCount = 4,
                Ingredients = new List<ResourceId> { s_plank, s_plank },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new(3, 3);
            grid.SetSlot(2, 2, s_plank);
            grid.SetSlot(0, 1, s_plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(s_stick, match.ResultItem);
            Assert.AreEqual(4, match.ResultCount);
        }

        [Test]
        public void FindMatch_ShapelessRecipe_WrongCount_ReturnsNull()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "sticks_recipe"))
            {
                Type = RecipeType.Shapeless,
                ResultItem = s_stick,
                ResultCount = 4,
                Ingredients = new List<ResourceId> { s_plank, s_plank },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new(3, 3);
            grid.SetSlot(0, 0, s_plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_ShapelessRecipe_WrongItem_ReturnsNull()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "sticks_recipe"))
            {
                Type = RecipeType.Shapeless,
                ResultItem = s_stick,
                ResultCount = 4,
                Ingredients = new List<ResourceId> { s_plank, s_plank },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new(3, 3);
            grid.SetSlot(0, 0, s_plank);
            grid.SetSlot(1, 0, s_cobblestone);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_EmptyGrid_ReturnsNull()
        {
            RecipeEntry recipe = new(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = s_craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', s_plank } },
            };

            CraftingEngine engine = new(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new(3, 3);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_MultipleRecipes_FindsCorrectOne()
        {
            RecipeEntry tableRecipe = new(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = s_craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', s_plank } },
            };

            RecipeEntry pickaxeRecipe = new(new ResourceId("lithforge", "stone_pickaxe_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = s_stonePickaxe,
                ResultCount = 1,
                Pattern = new List<string> { "###", " | ", " | " },
                Keys = new Dictionary<char, ResourceId>
                {
                    { '#', s_cobblestone },
                    { '|', s_stick },
                },
            };

            CraftingEngine engine = new(
                new List<RecipeEntry> { tableRecipe, pickaxeRecipe });

            CraftingGrid grid = new(3, 3);
            grid.SetSlot(0, 0, s_cobblestone);
            grid.SetSlot(1, 0, s_cobblestone);
            grid.SetSlot(2, 0, s_cobblestone);
            grid.SetSlot(1, 1, s_stick);
            grid.SetSlot(1, 2, s_stick);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(s_stonePickaxe, match.ResultItem);
        }

        [Test]
        public void RecipeCount_ReturnsCorrectCount()
        {
            List<RecipeEntry> recipes = new()
            {
                new RecipeEntry(new ResourceId("lithforge", "r1")),
                new RecipeEntry(new ResourceId("lithforge", "r2")),
                new RecipeEntry(new ResourceId("lithforge", "r3")),
            };

            CraftingEngine engine = new(recipes);

            Assert.AreEqual(3, engine.RecipeCount);
        }
    }
}
