namespace LegodPause.Service.Network;

public class NetworkServer
{
    private static NetworkServer _instance;
    private static readonly object _lock = new object();

    public static NetworkServer Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new NetworkServer();
                    }
                }
            }

            return _instance;
        }
    }

    private NetworkServer()
    {
    }
}