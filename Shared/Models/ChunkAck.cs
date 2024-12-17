namespace Shared.Models;

public class ChunkAck
{
    public int BlockRow { get; set; }
    public int BlockCol { get; set; }
    public int ChunkId { get; set; }
    public int NodeId { get; set; }
    public bool IsReceived { get; set; }
}
