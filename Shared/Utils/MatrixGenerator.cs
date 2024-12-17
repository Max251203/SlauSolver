namespace Shared.Utils;

public static class MatrixGenerator
{
    public static (double[,] matrix, double[] vector) Generate(int rows, int cols)
    {
        var matrix = new double[rows, cols];
        var vector = new double[rows];
        Random rand = new Random();

        // Генерируем матрицу с диагональным преобладанием для лучшей обусловленности
        for (int i = 0; i < rows; i++)
        {
            double rowSum = 0;
            for (int j = 0; j < cols; j++)
            {
                if (i != j)
                {
                    matrix[i, j] = rand.NextDouble() * 10;
                    rowSum += Math.Abs(matrix[i, j]);
                }
            }
            // Обеспечиваем диагональное преобладание
            matrix[i, i] = rowSum + rand.NextDouble() * 10 + 1;
            vector[i] = rand.NextDouble() * 10;
        }

        return (matrix, vector);
    }
}
