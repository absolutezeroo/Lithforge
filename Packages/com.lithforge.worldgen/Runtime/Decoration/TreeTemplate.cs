using System.Collections.Generic;

using Lithforge.Voxel.Block;

using Unity.Mathematics;

namespace Lithforge.WorldGen.Decoration
{
    public static class TreeTemplate
    {
        public static TreeBlock[] OakTree(StateId logId, StateId leavesId)
        {
            // Oak tree: 5-block trunk + 5x5x3 leaf canopy + 3x3x1 leaf top
            // Total: trunk (5) + canopy layer 1-3 (75) + top (9) - overlaps with trunk
            // Trunk is at center (0, y, 0) for y=0..4
            // Canopy at y=3..5 is 5x5, top at y=6 is 3x3

            int trunkHeight = 5;
            int canopyStartY = 3;
            int canopyEndY = 5;
            int topY = 6;

            // Count blocks
            int count = trunkHeight;  // trunk
            count += 3 * (5 * 5 - 1); // 3 layers of 5x5 minus trunk center
            count += 3 * 3 - 1;       // top 3x3 minus trunk would be gone but trunk is only 5 tall

            // Just build the list dynamically
            List<TreeBlock> blocks = new();

            // Trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                blocks.Add(new TreeBlock
                {
                    Offset = new int3(0, y, 0), State = logId,
                });
            }

            // Canopy layers (5x5)
            for (int y = canopyStartY; y <= canopyEndY; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        if (x == 0 && z == 0 && y < trunkHeight)
                        {
                            continue; // trunk is already here
                        }

                        blocks.Add(new TreeBlock
                        {
                            Offset = new int3(x, y, z), State = leavesId,
                        });
                    }
                }
            }

            // Top layer (3x3)
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    blocks.Add(new TreeBlock
                    {
                        Offset = new int3(x, topY, z), State = leavesId,
                    });
                }
            }

            return blocks.ToArray();
        }

        /// <summary>
        ///     Tall, narrow tree variant — 7-block trunk, 3x3x3 canopy + 1x1 top.
        ///     Uses the same log/leaves StateIds as oak but with a different shape.
        ///     TreeTemplateIndex = 1.
        /// </summary>
        public static TreeBlock[] BirchTree(StateId logId, StateId leavesId)
        {
            List<TreeBlock> blocks = new();

            int trunkHeight = 7;

            // Trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                blocks.Add(new TreeBlock
                {
                    Offset = new int3(0, y, 0), State = logId,
                });
            }

            // Canopy (3x3 for 3 layers: y=5,6,7)
            for (int y = 5; y <= 7; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0 && y < trunkHeight)
                        {
                            continue;
                        }

                        blocks.Add(new TreeBlock
                        {
                            Offset = new int3(x, y, z), State = leavesId,
                        });
                    }
                }
            }

            // Top (single block)
            blocks.Add(new TreeBlock
            {
                Offset = new int3(0, 8, 0), State = leavesId,
            });

            return blocks.ToArray();
        }

        /// <summary>
        ///     Conical tree variant — 6-block trunk, tapering canopy (5x5 → 3x3 → 1x1).
        ///     Uses the same log/leaves StateIds as oak but with a different shape.
        ///     TreeTemplateIndex = 2.
        /// </summary>
        public static TreeBlock[] SpruceTree(StateId logId, StateId leavesId)
        {
            List<TreeBlock> blocks = new();

            int trunkHeight = 6;

            // Trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                blocks.Add(new TreeBlock
                {
                    Offset = new int3(0, y, 0), State = logId,
                });
            }

            // Wide canopy at y=2,3 (5x5)
            for (int y = 2; y <= 3; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        if (x == 0 && z == 0)
                        {
                            continue;
                        }

                        blocks.Add(new TreeBlock
                        {
                            Offset = new int3(x, y, z), State = leavesId,
                        });
                    }
                }
            }

            // Mid canopy at y=4,5 (3x3)
            for (int y = 4; y <= 5; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0 && y < trunkHeight)
                        {
                            continue;
                        }

                        blocks.Add(new TreeBlock
                        {
                            Offset = new int3(x, y, z), State = leavesId,
                        });
                    }
                }
            }

            // Top (single block at y=6)
            blocks.Add(new TreeBlock
            {
                Offset = new int3(0, 6, 0), State = leavesId,
            });

            return blocks.ToArray();
        }
    }
}
