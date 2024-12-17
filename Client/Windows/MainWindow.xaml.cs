using Client.Services;
using Shared.Utils;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;

namespace Client.Windows;

public partial class MainWindow : Window
{
    private double[,] matrix;
    private double[] vector;
    private UdpClient udpClient;
    private CancellationTokenSource cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeControls()
    {
        ViewMatrixButton.IsEnabled = false;
        SolveButton.IsEnabled = false;
        ProgressBar.Value = 0;
        CancelButton.IsEnabled = false;
    }

    public void UpdateResults(string results)
    {
        ResultsTextBox.Clear();
        ResultsTextBox.AppendText(results);
        ResultsTextBox.ScrollToEnd();
    }

    private void GenerateMatrixButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MatrixSizeTextBox.Text, out int size))
        {
            MessageBox.Show("Введите корректный размер матрицы", "Ошибка");
            return;
        }

        try
        {
            (matrix, vector) = MatrixGenerator.Generate(size, size);
            MessageBox.Show("Матрица успешно сгенерирована", "Успех");
            ViewMatrixButton.IsEnabled = true;
            SolveButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при генерации матрицы: {ex.Message}", "Ошибка");
        }
    }

    private void ViewMatrixButton_Click(object sender, RoutedEventArgs e)
    {
        if (matrix == null || vector == null)
        {
            MessageBox.Show("Сначала сгенерируйте матрицу", "Предупреждение");
            return;
        }

        var matrixWindow = new MatrixViewWindow(matrix, vector);
        matrixWindow.Owner = this;
        matrixWindow.ShowDialog();
    }

    private async void SolveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput())
            return;

        try
        {
            DisableControls();
            ResultsTextBox.Clear();
            ProgressBar.Value = 0;

            cancellationTokenSource = new CancellationTokenSource();

            using (udpClient = new UdpClient(0))
            {
                var serverEndPoint = new IPEndPoint(
                    IPAddress.Parse(ServerIpTextBox.Text),
                    int.Parse(ServerPortTextBox.Text));

                var transmissionService = new MatrixTransmissionService(udpClient, serverEndPoint);
                var resultService = new ResultReceivingService(udpClient, matrix, vector);

                await transmissionService.SendMatrix(
                    matrix.GetLength(0),
                    matrix.GetLength(1),
                    int.Parse(NodesCountTextBox.Text));

                var result = await resultService.ReceiveResults();
            }
        }
        catch (OperationCanceledException)
        {
            ResultsTextBox.AppendText("Операция была отменена\n");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            ResultsTextBox.AppendText($"Ошибка: {ex.Message}\n");
        }
        finally
        {
            EnableControls();
            cancellationTokenSource?.Dispose();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
        ResultsTextBox.AppendText("Отмена операции...\n");
    }

    private bool ValidateInput()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ServerIpTextBox.Text) ||
                string.IsNullOrWhiteSpace(ServerPortTextBox.Text))
            {
                throw new Exception("Укажите IP адрес и порт сервера");
            }

            if (!IPAddress.TryParse(ServerIpTextBox.Text, out _))
            {
                throw new Exception("Некорректный IP адрес сервера");
            }

            if (!int.TryParse(ServerPortTextBox.Text, out int serverPort) ||
                serverPort <= 0 || serverPort > 65535)
            {
                throw new Exception("Некорректный порт сервера");
            }

            if (matrix == null || vector == null)
            {
                throw new Exception("Матрица не сгенерирована");
            }

            if (!int.TryParse(NodesCountTextBox.Text, out int nodesCount) ||
                nodesCount <= 0)
            {
                throw new Exception("Укажите корректное количество узлов");
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка валидации",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void DisableControls()
    {
        SolveButton.IsEnabled = false;
        GenerateMatrixButton.IsEnabled = false;
        ViewMatrixButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
    }

    private void EnableControls()
    {
        SolveButton.IsEnabled = true;
        GenerateMatrixButton.IsEnabled = true;
        ViewMatrixButton.IsEnabled = matrix != null;
        CancelButton.IsEnabled = false;
    }
} 