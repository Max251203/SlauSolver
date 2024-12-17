namespace Shared.Models;

public class NetworkMessages
{
    public class BlockTask
    {
        public string Type { get; set; }
        public int BlockRow { get; set; }
        public int BlockCol { get; set; }
        public MatrixBlock Matrix { get; set; }
        public double[] Vector { get; set; }
    }

    public class BlockResult
    {
        public string Type { get; set; }
        public int NodeId { get; set; }
        public int BlockRow { get; set; }
        public int BlockCol { get; set; }
        public MatrixBlock BlockData { get; set; }
    }

    public class BlockResultChunk
    {
        public int BlockRow { get; set; }
        public int BlockCol { get; set; }
        public int ChunkId { get; set; }
        public int TotalChunks { get; set; }
        public string Data { get; set; }
        public int NodeId { get; set; }
    }

    public class SolutionResult
    {
        public int MatrixSize { get; set; }
        public int NodesCount { get; set; }
        public double[] Solution { get; set; }
        public long DistributedTime { get; set; }
        public long SequentialTime { get; set; }
        public double MaxResidual { get; set; }
    }
}