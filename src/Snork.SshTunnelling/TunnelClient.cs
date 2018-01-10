using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Renci.SshNet;

namespace Snork.SshTunnelling
{
    public class TunnelClient
    {
        private static readonly Random PortNumberRandom = new Random();

        private static readonly TunnelClient _tunnelClient = new TunnelClient();

        private readonly List<ForwardedPortLocal> _forwardedPortLocals =
            new List<ForwardedPortLocal>();

        private SshClient _client;

        private TunnelClient()
        {
        }

        public bool ForwardedPortInfoExists(string remoteHost, uint remotePort)
        {
            return _forwardedPortLocals.Any(i => i.Host == remoteHost && i.Port == remotePort);
        }

        public static TunnelClient GetInstance()
        {
            return _tunnelClient;
        }

        public ForwardedPortLocal GetForwardedPortInfo(TunnelRequest request)
        {
            if (_client == null)
            {
                var uri = new Uri(request.SshTunnelInfo);
                if (uri.Scheme != "ssh")
                {
                    throw new ArgumentException("Invalid scheme, should be 'ssh'.");
                }
                var split = uri.UserInfo.Split(':');
                var username = split[0].Trim();
                var password = split[1].Trim();
                _client = new SshClient(uri.Host, uri.Port == -1 ? 22 : uri.Port, username, password)
                {
                    KeepAliveInterval = request.KeepAliveInterval,
                    ConnectionInfo = {Timeout = request.ConnectionTimeout}
                };
                _client.Connect();
            }
            var forwardedPortLocal = _forwardedPortLocals.FirstOrDefault(i => i.Host == request.RemoteHost &&
                                                                              i.Port == request.RemotePort);
            if (forwardedPortLocal == null)
            {
                while (true)
                {
                    var localPort = Convert.ToUInt32(PortNumberRandom.Next(49152, 65535));
                    try
                    {
                        forwardedPortLocal = new ForwardedPortLocal("127.0.0.1",
                            localPort, request.RemoteHost, request.RemotePort);
                        _client.AddForwardedPort(forwardedPortLocal);
                        forwardedPortLocal.Start();

                        _forwardedPortLocals.Add(forwardedPortLocal);
                        return forwardedPortLocal;
                    }
                    catch (SocketException socketException)
                    {
                        //port already used.  just ignore it and try another
                        if (socketException.ErrorCode != 10013)
                        {
                            throw;
                        }
                    }
                }
            }
            return forwardedPortLocal;
        }
    }
}