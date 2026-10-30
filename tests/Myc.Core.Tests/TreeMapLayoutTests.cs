using Myc.Core.Display;

namespace Myc.Core.Tests;

public class TreeMapLayoutTests
{
    [Fact]
    public void One_item_fills_the_rectangle()
    {
        IReadOnlyList<MapBlock> blocks = TreeMapLayout.Arrange(8, 4, [10]);

        Assert.Equal([new MapBlock(0, 0, 8, 4)], blocks);
    }

    [Theory]
    [InlineData(20, 10, new long[] { 50, 25, 20, 5 })]
    [InlineData(8, 4, new long[] { 1, 1 })]
    [InlineData(5, 5, new long[] { 9, 1 })]
    [InlineData(30, 12, new long[] { 100, 40, 40, 10, 5, 3, 2 })]
    [InlineData(6, 2, new long[] { 0, 0, 0 })]
    public void Blocks_cover_the_rectangle_without_overlap(int width, int height, long[] sizes)
    {
        IReadOnlyList<MapBlock> blocks = TreeMapLayout.Arrange(width, height, sizes);
        var painted = new int[height, width];
        foreach (MapBlock block in blocks)
        {
            Assert.InRange(block.X, 0, width);
            Assert.InRange(block.Y, 0, height);
            Assert.InRange(block.X + block.Width, 0, width);
            Assert.InRange(block.Y + block.Height, 0, height);
            for (int y = block.Y; y < block.Y + block.Height; y++)
            {
                for (int x = block.X; x < block.X + block.Width; x++)
                {
                    painted[y, x]++;
                }
            }
        }

        foreach (int cell in painted)
        {
            Assert.Equal(1, cell);
        }
    }

    [Fact]
    public void A_larger_weight_receives_more_cells()
    {
        IReadOnlyList<MapBlock> blocks = TreeMapLayout.Arrange(40, 20, [80, 20]);

        int large = blocks[0].Width * blocks[0].Height;
        int small = blocks[1].Width * blocks[1].Height;
        Assert.True(large > small);
    }

    [Fact]
    public void An_empty_list_or_a_zero_rectangle_draws_nothing()
    {
        Assert.Empty(TreeMapLayout.Arrange(0, 4, [1]));
        Assert.Empty(TreeMapLayout.Arrange(4, 4, []));
    }
}
