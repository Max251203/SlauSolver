namespace Shared.Models;

public class MatrixBlock
{
    public double[] Data { get; set; }
    public int Rows { get; set; }
    public int Cols { get; set; }

    public static MatrixBlock FromMatrix(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        double[] data = new double[rows * cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                data[i * cols + j] = matrix[i, j];
            }
        }

        return new MatrixBlock
        {
            Data = data,
            Rows = rows,
            Cols = cols
        };
    }

    public double[,] ToMatrix()
    {
        double[,] matrix = new double[Rows, Cols];
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Cols; j++)
            {
                matrix[i, j] = Data[i * Cols + j];
            }
        }
        return matrix;
    }
}
