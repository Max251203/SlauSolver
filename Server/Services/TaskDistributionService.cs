using Node.Services;
using Shared.Models;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
namespace Server.Services;

public class TaskDistributionService
{
    private readonly Dictionary<(int row, int col), bool> pendingBlocks = new();
    private readonly Dictionary<int, HashSet<(int row, int col)>> nodeBlocks = new();
    private BlockMatrix blockMatrix;
    private double[] vector;

    public void Initialize(BlockMatrix matrix, double[] vector)
    {
        this.blockMatrix = matrix;
        this.vector = vector;
        pendingBlocks.Clear();
        nodeBlocks.Clear();
    }

    public NetworkMessages.BlockTask CreateBlockTask(int row, int col, BlockMatrix matrix = null)
    {
        if (matrix != null)
        {
            blockMatrix = matrix;
        }

        var (block, rows, cols) = blockMatrix.GetBlock(row, col);
        double[] vectorPart = new double[rows];
        int startRow = row * blockMatrix.BlockSize;
        int vectorLength = Math.Min(rows, vector.Length - startRow);
        Array.Copy(vector, startRow, vectorPart, 0, vectorLength);

        return new NetworkMessages.BlockTask
        {
            Type = "TASK",
            BlockRow = row,
            BlockCol = col,
            Matrix = MatrixBlock.FromMatrix(block),
            Vector = vectorPart
        };
    }

    public bool IsBlockPending((int row, int col) block)
    {
        return pendingBlocks.ContainsKey(block) && pendingBlocks[block];
    }

    public void MarkBlockComplete((int row, int col) block)
    {
        if (pendingBlocks.ContainsKey(block))
        {
            pendingBlocks[block] = false;
        }
    }

    public void SetProcessingComplete()
    {
        pendingBlocks.Clear();
        nodeBlocks.Clear();
    }

    public HashSet<(int row, int col)> GetNodeBlocks(int nodeId)
    {
        return nodeBlocks.ContainsKey(nodeId)
            ? new HashSet<(int row, int col)>(nodeBlocks[nodeId])
            : new HashSet<(int row, int col)>();
    }

    public void AssignBlockToNode(int nodeId, (int row, int col) block)
    {
        if (!nodeBlocks.ContainsKey(nodeId))
        {
            nodeBlocks[nodeId] = new HashSet<(int row, int col)>();
        }
        nodeBlocks[nodeId].Add(block);
        pendingBlocks[block] = true;
    }
} 