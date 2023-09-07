using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Hazelnut.Web.Configurations;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ServerConfiguration
{
    public IPAddress BindAddress;
    public ushort Port;

    public bool UseTls;
    public X509Certificate? TlsCertificate;

    public string ServerName = "Hazelnut.Web";

    public ServerConfiguration(
        IPAddress bindAddress, ushort port,
        bool useTls = false,
        X509Certificate? tlsCertificate = null)
    {
        BindAddress = bindAddress;
        Port = port;

        UseTls = useTls;
        TlsCertificate = tlsCertificate;
    }
}