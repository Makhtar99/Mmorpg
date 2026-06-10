public enum NetworkSessionMode
{
    None,
    Host,
    Join,
}

public readonly struct NetworkSessionRequestData
{
    public NetworkSessionRequestData(NetworkSessionMode mode, string serverIp)
    {
        Mode = mode;
        ServerIp = serverIp;
    }

    public NetworkSessionMode Mode { get; }
    public string ServerIp { get; }
    public bool HasRequest => Mode != NetworkSessionMode.None;
}

public static class NetworkSessionRequest
{
    public static NetworkSessionRequestData Current { get; private set; } =
        new NetworkSessionRequestData(NetworkSessionMode.None, "127.0.0.1");

    public static void Host()
    {
        Current = new NetworkSessionRequestData(NetworkSessionMode.Host, "127.0.0.1");
    }

    public static void Join(string serverIp)
    {
        Current = new NetworkSessionRequestData(NetworkSessionMode.Join, NormalizeIp(serverIp));
    }

    public static NetworkSessionRequestData Consume()
    {
        NetworkSessionRequestData request = Current;
        Clear();
        return request;
    }

    public static void Clear()
    {
        Current = new NetworkSessionRequestData(NetworkSessionMode.None, "127.0.0.1");
    }

    private static string NormalizeIp(string serverIp)
    {
        return string.IsNullOrWhiteSpace(serverIp) ? "127.0.0.1" : serverIp.Trim();
    }
}
