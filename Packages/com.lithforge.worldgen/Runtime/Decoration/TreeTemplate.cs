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
            int count = trunkHeight; // trunk
            count += 3 * (5 * 5 - 1); // 3 layers of 5x5 minus trunk center
            count += 3 * 3 - 1; // top 3x3 minus trunk would be gone but trunk is only 5 tall

            // Just build the list dynamically
            System.Collections.Generic.List<TreeBlock> blocks =
                new System.Collections.Generic.List<TreeBlock>();

            // Trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                blocks.Add(new TreeBlock
                {
                    Offset = new int3(0, y, 0),
                    State = logId,
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
                            Offset = new int3(x, y, z),
                            State = leavesId,
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
                        Offset = new int3(x, topY, z),
                        State = leavesId,
                    });
                }
            }

            return blocks.ToArray();
        }
    }
}
