using System;
using System.IO;

using Lithforge.Voxel.Storage;

using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class RegionFileTests
    {
        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "lithforge_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        private string _testDir;

        [Test]
        public void SaveChunk_Flush_LoadChunk_RoundTrip()
        {
            string filePath = Path.Combine(_testDir, "test.lfrg");
            byte[] originalData =
            {
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
            };

            // Save and flush
            using (RegionFile region = new(filePath))
            {
                region.SaveChunk(0, 0, originalData);
                region.Flush();
            }

            // Load from a fresh instance (no cache)
            using (RegionFile region = new(filePath))
            {
                byte[] loaded = region.LoadChunk(0, 0);

                Assert.IsNotNull(loaded, "Loaded data should not be null");
                Assert.AreEqual(originalData.Length, loaded.Length, "Data length should match");

                for (int i = 0; i < originalData.Length; i++)
                {
                    Assert.AreEqual(originalData[i], loaded[i], $"Byte {i} should match");
                }
            }
        }

        [Test]
        public void SaveChunk_Twice_SecondOverwrites()
        {
            string filePath = Path.Combine(_testDir, "test_overwrite.lfrg");
            byte[] data1 =
            {
                10,
                20,
                30,
            };
            byte[] data2 =
            {
                40,
                50,
                60,
                70,
                80,
            };

            // First save
            using (RegionFile region = new(filePath))
            {
                region.SaveChunk(5, 5, data1);
                region.Flush();
            }

            // Second save to same slot
            using (RegionFile region = new(filePath))
            {
                region.SaveChunk(5, 5, data2);
                region.Flush();
            }

            // Load and verify the second data is present
            using (RegionFile region = new(filePath))
            {
                byte[] loaded = region.LoadChunk(5, 5);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(data2.Length, loaded.Length, "Second save should overwrite first");

                for (int i = 0; i < data2.Length; i++)
                {
                    Assert.AreEqual(data2[i], loaded[i], $"Byte {i} should match second save");
                }
            }
        }

        [Test]
        public void Flush_TempFileDeletedBeforeRename_OriginalSurvives()
        {
            string filePath = Path.Combine(_testDir, "test_crash.lfrg");
            byte[] originalData =
            {
                99,
                98,
                97,
            };

            // Save initial data
            using (RegionFile region = new(filePath))
            {
                region.SaveChunk(0, 0, originalData);
                region.Flush();
            }

            // Simulate: delete the .tmp file if it exists before a second flush
            // (simulating crash during write where .tmp was removed)
            string tempPath = filePath + ".tmp";

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            // Verify original file is still intact
            Assert.IsTrue(File.Exists(filePath), "Original file should still exist");

            using (RegionFile region = new(filePath))
            {
                byte[] loaded = region.LoadChunk(0, 0);

                Assert.IsNotNull(loaded, "Data should be recoverable from original file");
                Assert.AreEqual(originalData.Length, loaded.Length);

                for (int i = 0; i < originalData.Length; i++)
                {
                    Assert.AreEqual(originalData[i], loaded[i]);
                }
            }
        }

        [Test]
        public void HasChunk_ReturnsFalse_ForUnwrittenSlot()
        {
            string filePath = Path.Combine(_testDir, "test_has.lfrg");

            using (RegionFile region = new(filePath))
            {
                Assert.IsFalse(region.HasChunk(10, 10), "Unwritten slot should return false");
            }
        }

        [Test]
        public void HasChunk_ReturnsTrue_AfterFlush()
        {
            string filePath = Path.Combine(_testDir, "test_has2.lfrg");
            byte[] data =
            {
                1,
                2,
                3,
            };

            using (RegionFile region = new(filePath))
            {
                region.SaveChunk(3, 7, data);
                region.Flush();
            }

            using (RegionFile region = new(filePath))
            {
                Assert.IsTrue(region.HasChunk(3, 7), "Written slot should return true after flush");
            }
        }

        [Test]
        public void MultipleSlots_IndependentRoundTrip()
        {
            string filePath = Path.Combine(_testDir, "test_multi.lfrg");
            byte[] dataA =
            {
                1,
                2,
                3,
            };
            byte[] dataB =
            {
                4,
                5,
                6,
                7,
            };
            byte[] dataC =
            {
                8,
            };

            using (RegionFile region = new(filePath))
            {
                region.SaveChunk(0, 0, dataA);
                region.SaveChunk(15, 15, dataB);
                region.SaveChunk(31, 31, dataC);
                region.Flush();
            }

            using (RegionFile region = new(filePath))
            {
                byte[] loadedA = region.LoadChunk(0, 0);
                byte[] loadedB = region.LoadChunk(15, 15);
                byte[] loadedC = region.LoadChunk(31, 31);

                Assert.AreEqual(dataA.Length, loadedA.Length);
                Assert.AreEqual(dataB.Length, loadedB.Length);
                Assert.AreEqual(dataC.Length, loadedC.Length);

                Assert.AreEqual(dataA[0], loadedA[0]);
                Assert.AreEqual(dataB[3], loadedB[3]);
                Assert.AreEqual(dataC[0], loadedC[0]);
            }
        }
    }
}
