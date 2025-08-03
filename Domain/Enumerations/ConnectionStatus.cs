namespace Domain.Enumerations
{
    public enum ConnectionStatus
    {
        Disconnected = 1,
        Connecting = 2,
        Connected = 3,
        Authenticating = 4,
        Authenticated = 5,
        Failed = 6,
        Timeout = 7
    }
}
