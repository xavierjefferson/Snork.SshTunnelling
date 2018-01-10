using System;
using System.Collections.Generic;
using MongoDB.Driver;

namespace Snork.SshTunnelling.MongoDb
{
    public class Virtualizer
    {
        private static readonly Dictionary<string, ConnectionInfo> Clients =
            new Dictionary<string, ConnectionInfo>();

        private static readonly object Mutex = new object();
        private static readonly Virtualizer Instance = new Virtualizer();

        private Virtualizer()
        {
        }

        public static Virtualizer GetInstance()
        {
            return Instance;
        }

        public IMongoClient GetConnection(string connectionString, string sshTunnelInfo)
        {
            return Virtualize(connectionString, sshTunnelInfo).Client;
        }

        public string GetVirtualizedConnectionString(string connectionString, string sshTunnelInfo)
        {
            return Virtualize(connectionString, sshTunnelInfo).VirtualizedConnectionString;
        }

        private ConnectionInfo Virtualize(string connectionString, string sshTunnelInfo)
        {
            lock (Mutex)
            {
                if (Clients.ContainsKey(connectionString))
                {
                    return Clients[connectionString];
                }

                var virtualizedConnectionString = connectionString;
                var url = MongoUrl.Create(connectionString);
                IMongoClient client = null;
                if (!TunnelClient.GetInstance()
                    .ForwardedPortInfoExists(url.Server.Host, Convert.ToUInt32(url.Server.Port)))
                {
                    client = new MongoClient(connectionString);
                    if (!Ping(client))
                    {
                        client = null;
                    }
                }
                if (client == null)
                {
                    var tunnelRequest = new TunnelRequest
                    {
                        RemoteHost = url.Server.Host,
                        RemotePort = Convert.ToUInt32(url.Server.Port), SshTunnelInfo = sshTunnelInfo
                    };
                    var portInfo = TunnelClient.GetInstance().GetForwardedPortInfo(tunnelRequest);
                    var newConnectionString = string.Format("mongodb://{2}{3}:{0}/{1}", portInfo.BoundPort,
                        url.DatabaseName,
                        string.IsNullOrWhiteSpace(url.Username)
                            ? string.Empty
                            : string.Concat(url.Username, ":", url.Password, "@"), portInfo.BoundHost);
                    virtualizedConnectionString = newConnectionString;
                    client = new MongoClient(newConnectionString);
                    if (!Ping(client))
                    {
                        throw new Exception("Can't connect to MongoDb");
                    }
                }
                var connectionInfo =
                    new ConnectionInfo
                    {
                        VirtualizedConnectionString = virtualizedConnectionString,
                        Client = client
                    };

                Clients[connectionString] = connectionInfo;
                return connectionInfo;
            }
        }

        private bool Ping(IMongoClient mongoClient)
        {
            try
            {
                var a = mongoClient.GetDatabase("_tmp");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public class ConnectionInfo
        {
            public IMongoClient Client { get; set; }
            public string VirtualizedConnectionString { get; set; }
        }
    }
}