using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LegodPause.Core.Network;

public class NetworkUtil
{
    public static List<IPAddress> GetAllIP()
    {
        List<IPAddress> addresses = new List<IPAddress>()
        {
            new([127, 0, 0, 1])
        };
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                {
                    if (!addresses.Contains(ip.Address))
                    {
                        addresses.Add(ip.Address);
                    }
                }
            }
        }

        return addresses;
    }

    public static List<IPAddress> GetAllIPBradcast()
    {
        List<IPAddress> addresses = new List<IPAddress>()
        {
            new([127, 0, 0, 1])
        };

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] ipBytes = ip.Address.GetAddressBytes();
                    byte[] maskBytes = ip.IPv4Mask.GetAddressBytes();
                    byte[] broadcastBytes = new byte[4];

                    for (int i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                    }

                    IPAddress broadcastAddress = new IPAddress(broadcastBytes);
                    if (!addresses.Contains(broadcastAddress))
                    {
                        addresses.Add(broadcastAddress);
                    }
                }
                else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    byte[] ipBytes = ip.Address.GetAddressBytes();
                    byte[] prefixLength = BitConverter.GetBytes(ip.PrefixLength);
                    byte[] broadcastBytes = new byte[16];

                    for (int i = 0; i < 16; i++)
                    {
                        int maskBit = (i < prefixLength[0] / 8) ? 0xFF :
                            (i == prefixLength[0] / 8) ? (0xFF << (8 - (prefixLength[0] % 8))) : 0x00;
                        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBit);
                    }

                    IPAddress broadcastAddress = new IPAddress(broadcastBytes);
                    if (!addresses.Contains(broadcastAddress))
                    {
                        addresses.Add(broadcastAddress);
                    }
                }
            }
        }

        return addresses;
    }
}