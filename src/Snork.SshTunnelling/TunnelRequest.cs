using System;

namespace Snork.SshTunnelling
{
    public class TunnelRequest
    {
        public string RemoteHost { get; set; }
        public uint RemotePort { get; set; }
        public string SshTunnelInfo { get; set; }
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20);
    }
}