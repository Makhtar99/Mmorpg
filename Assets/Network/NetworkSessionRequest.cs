public enum NetworkSessionMode
{
    None,
    Host,
    Join,
}

public readonly struct NetworkSessionRequestData
{
    public NetworkSessionRequestData(NetworkSessionMode mode, string serverIp)
        : this(mode, serverIp, 0, PlayerNames.DefaultName)
    {
    }

    public NetworkSessionRequestData(NetworkSessionMode mode, string serverIp, byte character)
        : this(mode, serverIp, character, PlayerNames.DefaultName)
    {
    }

    public NetworkSessionRequestData(NetworkSessionMode mode, string serverIp, byte character, string playerName)
    {
        Mode = mode;
        ServerIp = serverIp;
        Character = character;
        PlayerName = PlayerNames.Normalize(playerName);
    }

    public NetworkSessionMode Mode { get; }
    public string ServerIp { get; }
    public byte Character { get; }
    public string PlayerName { get; }
    public bool HasRequest => Mode != NetworkSessionMode.None;
}

public static class NetworkSessionRequest
{
    public static NetworkSessionRequestData Current { get; private set; } =
        new NetworkSessionRequestData(NetworkSessionMode.None, "127.0.0.1");

    public static void Host()
    {
        Host(0, PlayerNames.DefaultName);
    }

    public static void Host(byte character)
    {
        Host(character, PlayerNames.DefaultName);
    }

    public static void Host(byte character, string playerName)
    {
        Current = new NetworkSessionRequestData(NetworkSessionMode.Host, "127.0.0.1", character, playerName);
    }

    public static void Join(string serverIp)
    {
        Join(serverIp, 0, PlayerNames.DefaultName);
    }

    public static void Join(string serverIp, byte character)
    {
        Join(serverIp, character, PlayerNames.DefaultName);
    }

    public static void Join(string serverIp, byte character, string playerName)
    {
        Current = new NetworkSessionRequestData(NetworkSessionMode.Join, NormalizeIp(serverIp), character, playerName);
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
