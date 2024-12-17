using Shared.Network;

namespace Shared.Solvers;

public class GaussSolver
{
    public double[] Solve(double[,] matrix, double[] vector)
    {
        int n = vector.Length;
        double[,] augmentedMatrix = Utils.MatrixUtils.CreateAugmentedMatrix(matrix, vector);

        // Прямой ход
        for (int i = 0; i < n; i++)
        {
            // Выбор главного элемента
            int maxRow = i;
            for (int k = i + 1; k < n; k++)
            {
                if (Math.Abs(augmentedMatrix[k, i]) > Math.Abs(augmentedMatrix[maxRow, i]))
                    maxRow = k;
            }

            // Перестановка строк
            if (maxRow != i)
            {
                for (int j = i; j <= n; j++)
                {
                    (augmentedMatrix[i, j], augmentedMatrix[maxRow, j]) =
                        (augmentedMatrix[maxRow, j], augmentedMatrix[i, j]);
                }
            }

            // Приведение к треугольному виду
            for (int k = i + 1; k < n; k++)
            {
                double factor = augmentedMatrix[k, i] / augmentedMatrix[i, i];
                for (int j = i; j <= n; j++)
                    augmentedMatrix[k, j] -= factor * augmentedMatrix[i, j];
            }
        }

        // Обратный ход
        double[] solution = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            solution[i] = augmentedMatrix[i, n];
            for (int j = i + 1; j < n; j++)
                solution[i] -= augmentedMatrix[i, j] * solution[j];
            solution[i] /= augmentedMatrix[i, i];
        }

        return solution;
    }
} 