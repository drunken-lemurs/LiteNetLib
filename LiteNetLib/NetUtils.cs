using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace LiteNetLib
{
    /// <summary>
    /// Address type that you want to receive from NetUtils.GetLocalIp method
    /// </summary>
    [Flags]
    public enum LocalAddrType
    {
        IPv4 = 1,
        IPv6 = 2,
        All = IPv4 | IPv6
    }

    /// <summary>
    /// Some specific network utilities
    /// </summary>
    public static class NetUtils
    {
        public static IPEndPoint MakeEndPoint(string hostStr, int port)
        {
            return new IPEndPoint(ResolveAddress(hostStr), port);
        }

        public static IPAddress ResolveAddress(string hostStr, AddressFamily addressFamily = AddressFamily.Unspecified)
        {
            if (addressFamily == AddressFamily.InterNetworkV6)
                if (!NetSocket.IPv6Support)
                {
                    if (string.IsNullOrEmpty(hostStr))
                        return IPAddress.None;

                    throw new InvalidOperationException("IPV6 support is disabled.");
                }


            IPAddress ipAddress;
            if (IPAddress.TryParse(hostStr, out ipAddress))
            {
                if (addressFamily == AddressFamily.Unspecified || ipAddress.AddressFamily == addressFamily)
                    return ipAddress;

                throw new ArgumentException("Invalid address: " + hostStr, "hostStr");
            }
            else
            {
                foreach (IPAddress ip in ResolveAddresses(hostStr))
                    if ((addressFamily == AddressFamily.Unspecified && ip.AddressFamily > AddressFamily.Unspecified) || (ip.AddressFamily == addressFamily))
                        return ip;
            }

            throw new SocketException((int)SocketError.HostNotFound);
        }

        private static IPAddress[] ResolveAddresses(string hostStr)
        {
#if NETSTANDARD2_0 || NETCOREAPP2_0
            var hostTask = Dns.GetHostEntryAsync(hostStr);
            hostTask.GetAwaiter().GetResult();
            var host = hostTask.Result;
#else
            var host = Dns.GetHostEntry(hostStr);
#endif
            return host.AddressList;
        }

        /// <summary>
        /// Get all local ip addresses
        /// </summary>
        /// <param name="addrType">type of address (IPv4, IPv6 or both)</param>
        /// <returns>List with all local ip adresses</returns>
        public static List<IPAddress> GetLocalIpList(LocalAddrType addrType)
        {
            List<IPAddress> targetList = new List<IPAddress>();
            GetLocalIpList(targetList, addrType);
            return targetList;
        }

        /// <summary>
        /// Get all local ip addresses (non alloc version)
        /// </summary>
        /// <param name="targetList">result list</param>
        /// <param name="addrType">type of address (IPv4, IPv6 or both)</param>
        public static void GetLocalIpList(List<IPAddress> targetList, LocalAddrType addrType)
        {
            bool ipv4 = (addrType & LocalAddrType.IPv4) == LocalAddrType.IPv4;
            bool ipv6 = (addrType & LocalAddrType.IPv6) == LocalAddrType.IPv6;
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    //Skip loopback and disabled network interfaces
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || 
                        ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    var ipProps = ni.GetIPProperties();

                    //Skip address without gateway
                    if (ipProps.GatewayAddresses.Count == 0)
                        continue;

                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        var address = ip.Address;
                        if ((ipv4 && address.AddressFamily == AddressFamily.InterNetwork) ||
                            (ipv6 && address.AddressFamily == AddressFamily.InterNetworkV6))
                            targetList.Add(address);
                    }
                }
            }
            catch
            {
                //ignored
            }

            //Fallback mode (unity android)
            if (targetList.Count == 0)
            {
                IPAddress[] addresses = ResolveAddresses(Dns.GetHostName());
                foreach (IPAddress ip in addresses)
                {
                    if((ipv4 && ip.AddressFamily == AddressFamily.InterNetwork) ||
                       (ipv6 && ip.AddressFamily == AddressFamily.InterNetworkV6))
                        targetList.Add(ip);
                }
            }
            if (targetList.Count == 0)
            {
                if(ipv4)
                    targetList.Add(IPAddress.Loopback);
                if(ipv6)
                    targetList.Add(IPAddress.IPv6Loopback);
            }
        }

        private static readonly List<IPAddress> IpList = new List<IPAddress>();
        /// <summary>
        /// Get first detected local ip address
        /// </summary>
        /// <param name="addrType">type of address (IPv4, IPv6 or both)</param>
        /// <returns>IP address if available. Else - string.Empty</returns>
        public static IPAddress GetLocalIp(LocalAddrType addrType)
        {
            lock (IpList)
            {
                IpList.Clear();
                GetLocalIpList(IpList, addrType);
                return IpList.Count == 0 ? IPAddress.None : IpList[0];
            }
        }

        // ===========================================
        // Internal and debug log related stuff
        // ===========================================
        internal static void PrintInterfaceInfos()
        {
            NetDebug.WriteForce(NetLogLevel.Info, "IPv6Support: {0}", NetSocket.IPv6Support);
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork ||
                            ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            NetDebug.WriteForce(
                                NetLogLevel.Info,
                                "Interface: {0}, Type: {1}, Ip: {2}, OpStatus: {3}",
                                ni.Name,
                                ni.NetworkInterfaceType.ToString(),
                                ip.Address.ToString(),
                                ni.OperationalStatus.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                NetDebug.WriteForce(NetLogLevel.Info, "Error while getting interface infos: {0}", e.ToString());
            }
        }

        internal static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + NetConstants.MaxSequence + NetConstants.HalfMaxSequence) % NetConstants.MaxSequence - NetConstants.HalfMaxSequence;
        }
    }
}
