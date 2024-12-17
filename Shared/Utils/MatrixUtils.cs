namespace Shared.Utils;

public static class MatrixUtils
{
    public static double[,] CreateAugmentedMatrix(double[,] matrix, double[] vector)
    {
        int n = vector.Length;
        double[,] augmented = new double[n, n + 1];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                augmented[i, j] = matrix[i, j];
            augmented[i, n] = vector[i];
        }

        return augmented;
    }
}