using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Snork.SshTunnelling.MySQL
{
    public class Virtualizer
    {
        private static readonly Dictionary<string, ConnectionInfo> Clients =
            new Dictionary<string, ConnectionInfo>();

        private static readonly object LockObject = new object();

        private static readonly Virtualizer Instance = new Virtualizer();

        private Virtualizer()
        {
        }

        public static Virtualizer GetInstance()
        {
            return Instance;
        }


        public string GetVirtualizedConnectionString(string connectionString, string sshTunnelInfo)
        {
            return Virtualize(connectionString, sshTunnelInfo).VirtualizedConnectionString;
        }

        private ConnectionInfo Virtualize(string connectionString, string sshTunnelInfo)
        {
            lock (LockObject)
            {
                if (Clients.ContainsKey(connectionString))
                {
                    return Clients[connectionString];
                }

                var url = new MySqlConnectionStringBuilder(connectionString);


                string finalConnectionString = null;
                if (!TunnelClient.GetInstance().ForwardedPortInfoExists(url.Server, Convert.ToUInt32(url.Port)))
                {
                    if (Ping(connectionString))
                    {
                        finalConnectionString = connectionString;
                    }
                }
                if (finalConnectionString == null)
                {
                    var portInfo = TunnelClient.GetInstance()
                        .GetForwardedPortInfo(
                            new TunnelRequest
                            {
                                RemoteHost = url.Server,
                                RemotePort = Convert.ToUInt32(url.Port),
                                SshTunnelInfo = sshTunnelInfo
                            });
                    var alternateBuilder =
                        new MySqlConnectionStringBuilder(connectionString)
                        {
                            Port = portInfo.BoundPort,
                            Server = portInfo.BoundHost
                        };


                    var newConnectionString = alternateBuilder.ToString();
                    if (Ping(newConnectionString))
                    {
                        finalConnectionString = newConnectionString;
                    }
                    else

                    {
                        throw new Exception("Can't connect to MySQL");
                    }
                }
                var connectionInfo =
                    new ConnectionInfo
                    {
                        VirtualizedConnectionString = finalConnectionString
                    };
                Clients[connectionString] = connectionInfo;
                return connectionInfo;
            }
        }

        private bool Ping(string connectionString)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public class ConnectionInfo
        {
            public string VirtualizedConnectionString { get; set; }
        }
    }
}