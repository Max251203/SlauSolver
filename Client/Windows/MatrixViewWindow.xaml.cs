using System.Data;
using System.Windows;

namespace Client.Windows;

public partial class MatrixViewWindow : Window
{
    private readonly double[,] matrix;
    private readonly double[] vector;

    public MatrixViewWindow(double[,] matrix, double[] vector)
    {
        InitializeComponent();
        this.matrix = matrix;
        this.vector = vector;
        LoadMatrixToGrid();
    }

    private void LoadMatrixToGrid()
    {
        try
        {
            var table = new DataTable();

            // Добавляем столбцы для матрицы
            int cols = matrix.GetLength(1);
            for (int j = 0; j < cols; j++)
            {
                table.Columns.Add($"X{j + 1}", typeof(double));
            }
            // Добавляем столбец для вектора правой части
            table.Columns.Add("B", typeof(double));

            // Заполняем данными
            int rows = matrix.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                var row = table.NewRow();
                for (int j = 0; j < cols; j++)
                {
                    row[j] = Math.Round(matrix[i, j], 6);
                }
                row[cols] = Math.Round(vector[i], 6);
                table.Rows.Add(row);
            }

            MatrixGrid.ItemsSource = table.DefaultView;
            Title = $"Просмотр матрицы [{rows}x{cols}]";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке матрицы: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}