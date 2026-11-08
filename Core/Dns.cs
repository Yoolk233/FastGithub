using System.Runtime.InteropServices;

namespace FastGithub;

public static class Dns
{
    [DllImport("dnsapi.dll")]
    private static extern int DnsFlushResolverCache();

    /// <summary>刷新 Windows DNS 缓存，让新的 hosts 立刻生效</summary>
    public static void Flush()
    {
        try
        {
            DnsFlushResolverCache();
        }
        catch
        {
        }
    }
}
