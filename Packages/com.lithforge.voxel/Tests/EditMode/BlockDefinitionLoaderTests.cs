using System.Collections.Generic;
using System.IO;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Content;
using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class BlockDefinitionLoaderTests
    {
        private string _tempDir;
        private BlockDefinitionLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lithforge_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "data", "lithforge", "blocks"));
            _loader = new BlockDefinitionLoader(NullLogger.Instance, new ContentValidator());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void LoadAll_Stone_LoadsCorrectly()
        {
            string json = @"{
                ""hardness"": 1.5,
                ""blast_resistance"": 6.0,
                ""collision_shape"": ""full_cube"",
                ""render_layer"": ""opaque"",
                ""light_emission"": 0,
                ""light_filter"": 15,
                ""sound_group"": ""stone""
            }";
            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "stone.json"), json);

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual("lithforge", defs[0].Id.Namespace);
            Assert.AreEqual("stone", defs[0].Id.Name);
            Assert.AreEqual(1.5, defs[0].Hardness);
            Assert.AreEqual(6.0, defs[0].BlastResistance);
            Assert.AreEqual("full_cube", defs[0].CollisionShape);
            Assert.AreEqual("opaque", defs[0].RenderLayer);
            Assert.AreEqual(15, defs[0].LightFilter);
        }

        [Test]
        public void LoadAll_OakLog_HasAxisProperty()
        {
            string json = @"{
                ""properties"": {
                    ""axis"": { ""type"": ""enum"", ""values"": [""x"", ""y"", ""z""], ""default"": ""y"" }
                },
                ""hardness"": 2.0,
                ""collision_shape"": ""full_cube"",
                ""render_layer"": ""opaque"",
                ""light_filter"": 15
            }";
            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "oak_log.json"), json);

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual(1, defs[0].Properties.Count);
            Assert.AreEqual("axis", defs[0].Properties[0].Name);
            Assert.AreEqual(3, defs[0].Properties[0].ValueCount);
            Assert.AreEqual(3, defs[0].ComputeStateCount());
        }

        [Test]
        public void LoadAll_Furnace_ProducesEightStates()
        {
            string json = @"{
                ""properties"": {
                    ""facing"": { ""type"": ""enum"", ""values"": [""north"", ""south"", ""east"", ""west""], ""default"": ""north"" },
                    ""lit"": { ""type"": ""bool"", ""default"": ""false"" }
                },
                ""hardness"": 3.5,
                ""collision_shape"": ""full_cube"",
                ""render_layer"": ""opaque"",
                ""light_filter"": 15
            }";
            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "furnace.json"), json);

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual(2, defs[0].Properties.Count);
            Assert.AreEqual(8, defs[0].ComputeStateCount()); // 4 facing × 2 lit
        }

        [Test]
        public void LoadAll_InvalidJson_SkipsFile()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "data", "lithforge", "blocks", "broken.json"),
                "{ not valid json }}}");

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(0, defs.Count);
        }

        [Test]
        public void LoadAll_InvalidRenderLayer_SkipsFile()
        {
            string json = @"{
                ""hardness"": 1.0,
                ""collision_shape"": ""full_cube"",
                ""render_layer"": ""invalid_layer"",
                ""light_filter"": 15
            }";
            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "bad.json"), json);

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(0, defs.Count);
        }

        [Test]
        public void LoadAll_EmptyDirectory_ReturnsEmpty()
        {
            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(0, defs.Count);
        }

        [Test]
        public void LoadAll_MultipleBlocks_LoadsAll()
        {
            string stoneJson = @"{ ""hardness"": 1.5, ""collision_shape"": ""full_cube"", ""render_layer"": ""opaque"", ""light_filter"": 15 }";
            string dirtJson = @"{ ""hardness"": 0.5, ""collision_shape"": ""full_cube"", ""render_layer"": ""opaque"", ""light_filter"": 15 }";

            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "stone.json"), stoneJson);
            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "dirt.json"), dirtJson);

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(2, defs.Count);
        }

        [Test]
        public void LoadAll_WithTags_ParsesTags()
        {
            string json = @"{
                ""hardness"": 1.0,
                ""collision_shape"": ""full_cube"",
                ""render_layer"": ""opaque"",
                ""light_filter"": 15,
                ""tags"": [""lithforge:mineable_pickaxe"", ""lithforge:base_stone""]
            }";
            File.WriteAllText(Path.Combine(_tempDir, "data", "lithforge", "blocks", "stone.json"), json);

            List<BlockDefinition> defs = _loader.LoadAll(_tempDir);

            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual(2, defs[0].Tags.Count);
            Assert.AreEqual("lithforge:mineable_pickaxe", defs[0].Tags[0]);
        }
    }
}
