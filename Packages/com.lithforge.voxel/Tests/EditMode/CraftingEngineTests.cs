using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Voxel.Crafting;
using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class CraftingEngineTests
    {
        private static readonly ResourceId _plank = new ResourceId("lithforge", "oak_planks");
        private static readonly ResourceId _stick = new ResourceId("lithforge", "stick");
        private static readonly ResourceId _craftingTable = new ResourceId("lithforge", "crafting_table");
        private static readonly ResourceId _stonePickaxe = new ResourceId("lithforge", "stone_pickaxe");
        private static readonly ResourceId _cobblestone = new ResourceId("lithforge", "cobblestone");

        [Test]
        public void FindMatch_ShapedRecipe_MatchesPattern()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = _craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', _plank } }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(0, 0, _plank);
            grid.SetSlot(1, 0, _plank);
            grid.SetSlot(0, 1, _plank);
            grid.SetSlot(1, 1, _plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(_craftingTable, match.ResultItem);
        }

        [Test]
        public void FindMatch_ShapedRecipe_WrongPattern_ReturnsNull()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = _craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', _plank } }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(0, 0, _plank);
            grid.SetSlot(1, 0, _plank);
            grid.SetSlot(2, 0, _plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_ShapedRecipe_OffsetPosition_Matches()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = _craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', _plank } }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            // Place pattern offset to bottom-right corner
            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(1, 1, _plank);
            grid.SetSlot(2, 1, _plank);
            grid.SetSlot(1, 2, _plank);
            grid.SetSlot(2, 2, _plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(_craftingTable, match.ResultItem);
        }

        [Test]
        public void FindMatch_ShapelessRecipe_AnyOrder_Matches()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "sticks_recipe"))
            {
                Type = RecipeType.Shapeless,
                ResultItem = _stick,
                ResultCount = 4,
                Ingredients = new List<ResourceId> { _plank, _plank }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(2, 2, _plank);
            grid.SetSlot(0, 1, _plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(_stick, match.ResultItem);
            Assert.AreEqual(4, match.ResultCount);
        }

        [Test]
        public void FindMatch_ShapelessRecipe_WrongCount_ReturnsNull()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "sticks_recipe"))
            {
                Type = RecipeType.Shapeless,
                ResultItem = _stick,
                ResultCount = 4,
                Ingredients = new List<ResourceId> { _plank, _plank }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(0, 0, _plank);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_ShapelessRecipe_WrongItem_ReturnsNull()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "sticks_recipe"))
            {
                Type = RecipeType.Shapeless,
                ResultItem = _stick,
                ResultCount = 4,
                Ingredients = new List<ResourceId> { _plank, _plank }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(0, 0, _plank);
            grid.SetSlot(1, 0, _cobblestone);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_EmptyGrid_ReturnsNull()
        {
            RecipeEntry recipe = new RecipeEntry(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = _craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', _plank } }
            };

            CraftingEngine engine = new CraftingEngine(new List<RecipeEntry> { recipe });

            CraftingGrid grid = new CraftingGrid(3, 3);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNull(match);
        }

        [Test]
        public void FindMatch_MultipleRecipes_FindsCorrectOne()
        {
            RecipeEntry tableRecipe = new RecipeEntry(new ResourceId("lithforge", "crafting_table_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = _craftingTable,
                ResultCount = 1,
                Pattern = new List<string> { "##", "##" },
                Keys = new Dictionary<char, ResourceId> { { '#', _plank } }
            };

            RecipeEntry pickaxeRecipe = new RecipeEntry(new ResourceId("lithforge", "stone_pickaxe_recipe"))
            {
                Type = RecipeType.Shaped,
                ResultItem = _stonePickaxe,
                ResultCount = 1,
                Pattern = new List<string> { "###", " | ", " | " },
                Keys = new Dictionary<char, ResourceId>
                {
                    { '#', _cobblestone },
                    { '|', _stick }
                }
            };

            CraftingEngine engine = new CraftingEngine(
                new List<RecipeEntry> { tableRecipe, pickaxeRecipe });

            CraftingGrid grid = new CraftingGrid(3, 3);
            grid.SetSlot(0, 0, _cobblestone);
            grid.SetSlot(1, 0, _cobblestone);
            grid.SetSlot(2, 0, _cobblestone);
            grid.SetSlot(1, 1, _stick);
            grid.SetSlot(1, 2, _stick);

            RecipeEntry match = engine.FindMatch(grid);

            Assert.IsNotNull(match);
            Assert.AreEqual(_stonePickaxe, match.ResultItem);
        }

        [Test]
        public void RecipeCount_ReturnsCorrectCount()
        {
            List<RecipeEntry> recipes = new List<RecipeEntry>
            {
                new RecipeEntry(new ResourceId("lithforge", "r1")),
                new RecipeEntry(new ResourceId("lithforge", "r2")),
                new RecipeEntry(new ResourceId("lithforge", "r3"))
            };

            CraftingEngine engine = new CraftingEngine(recipes);

            Assert.AreEqual(3, engine.RecipeCount);
        }
    }
}
