namespace MilOps.Services.Protocol;

public enum PacketType : byte
{
    AUTH = 1,
    AUTH_ACK = 2,
    MSG_SEND = 10,
    MSG_RECV = 11,
    MSG_ACK = 12,
    MSG_READ = 20,
    MSG_READ_ACK = 21,
    HEARTBEAT = 30,
    ERROR = 99
}
