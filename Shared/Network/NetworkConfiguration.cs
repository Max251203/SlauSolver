using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

namespace Shared.Network;

public static class NetworkConfiguration
{
    public static class Ports
    {
        public const int SERVER_PORT = 11000;
        public const int BASE_NODE_PORT = 11001;
        public static int GetNodePort(int nodeId) => BASE_NODE_PORT + nodeId;
    }

    public static class Timeouts
    {
        public const int DEFAULT_TIMEOUT_MS = 5000;
        public const int CHUNK_TRANSMISSION_DELAY_MS = 50;
        public const int NODE_INITIALIZATION_DELAY_MS = 200;
        public const int TASK_DISTRIBUTION_DELAY_MS = 100;
        public const int HEARTBEAT_INTERVAL_MS = 2000;
        public const int HEALTH_CHECK_INTERVAL_MS = 5000;
        public const int CHUNK_ACKNOWLEDGEMENT_TIMEOUT_MS = 3000;
        public const int UNACKNOWLEDGED_CHECK_INTERVAL_MS = 1000;
        public const int RETRY_BASE_DELAY_MS = 100;
        public const int RETRY_MAX_DELAY_MS = 1000;
    }

    public static class Sizes
    {
        public const int MAX_UDP_PACKET_SIZE = 60000;
        public const int RECEIVE_BUFFER_SIZE = 65536;
        public const int SEND_BUFFER_SIZE = 65536;
        public const int MIN_BLOCK_SIZE = 25;
        public const int MAX_BLOCK_SIZE = 100;
    }

    public static class Retry
    {
        public const int MAX_RETRIES = 3;
        public const int MAX_BLOCK_RETRIES = 2;
    }
}
