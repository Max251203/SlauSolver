namespace Shared.Models;

public class BlockMatrix
{
    private readonly double[,] matrix;
    private readonly int blockSize;
    private readonly int totalRows;
    private readonly int totalCols;
    private readonly int blocksInRow;
    private readonly int blocksInCol;

    public BlockMatrix(double[,] matrix, int blockSize)
    {
        this.matrix = matrix;
        this.blockSize = blockSize;
        this.totalRows = matrix.GetLength(0);
        this.totalCols = matrix.GetLength(1);

        this.blocksInRow = (int)Math.Ceiling((double)totalRows / blockSize);
        this.blocksInCol = (int)Math.Ceiling((double)totalCols / blockSize);
    }

    public (double[,] block, int actualRows, int actualCols) GetBlock(int blockRow, int blockCol)
    {
        int startRow = blockRow * blockSize;
        int startCol = blockCol * blockSize;

        int actualRows = Math.Min(blockSize, totalRows - startRow);
        int actualCols = Math.Min(blockSize, totalCols - startCol);

        if (actualRows <= 0 || actualCols <= 0)
        {
            throw new ArgumentException($"Некорректные размеры блока: {actualRows}x{actualCols}");
        }

        double[,] block = new double[actualRows, actualCols];

        for (int i = 0; i < actualRows; i++)
        {
            for (int j = 0; j < actualCols; j++)
            {
                block[i, j] = matrix[startRow + i, startCol + j];
            }
        }

        return (block, actualRows, actualCols);
    }

    public void SetBlock(double[,] block, int blockRow, int blockCol)
    {
        int startRow = blockRow * blockSize;
        int startCol = blockCol * blockSize;

        int rows = block.GetLength(0);
        int cols = block.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[startRow + i, startCol + j] = block[i, j];
            }
        }
    }

    public int BlocksInRow => blocksInRow;
    public int BlocksInCol => blocksInCol;
    public int BlockSize => blockSize;
    public double[,] Matrix => matrix;
}
