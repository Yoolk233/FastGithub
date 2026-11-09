using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FastGithub;

public static class Net
{
    private static readonly object httpLock = new object();
    private static HttpClient http;

    /// <summary>探测用的共享 HttpClient：不走系统代理，压缩传输</summary>
    public static HttpClient Http
    {
        get
        {
            if (http == null)
            {
                lock (httpLock)
                {
                    if (http == null)
                    {
                        http = new HttpClient(new HttpClientHandler
                        {
                            UseProxy = false,
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                        });
                        http.Timeout = TimeSpan.FromSeconds(5.0);
                    }
                }
            }
            return http;
        }
    }

    /// <summary>用阿里 / 腾讯 DoH 解析域名，返回空格分隔的 IP 列表</summary>
    public static string DoH(string domain)
    {
        string[] endpoints =
        {
            "https://dns.alidns.com/resolve?name={0}&type=A",
            "https://doh.pub/dns-query?name={0}&type=A"
        };
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < endpoints.Length; i++)
        {
            try
            {
                string requestUri = string.Format(endpoints[i], domain);
                MatchCollection matches = Regex.Matches(
                    Http.GetStringAsync(requestUri).Result,
                    "\"data\":\"([0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3})\"");
                for (int j = 0; j < matches.Count; j++)
                {
                    sb.Append(matches[j].Groups[1].Value).Append(' ');
                }
            }
            catch
            {
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>TCP 连接耗时（毫秒），不可达返回 -1</summary>
    public static long TcpDelay(string ip, int timeoutMs)
    {
        Stopwatch sw = Stopwatch.StartNew();
        TcpClient client = null;
        try
        {
            client = new TcpClient();
            client.NoDelay = true;
            IAsyncResult ar = client.BeginConnect(ip, 443, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
            {
                return -1L;
            }
            client.EndConnect(ar);
            return sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1L;
        }
        finally
        {
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>用默认复验路径做 TLS + HTTP 复验</summary>
    public static Node Verify(string host, string ip, int timeoutMs)
    {
        return Verify(host, ip, Config.ProbePath(host), timeoutMs);
    }

    /// <summary>TLS + HTTP 复验（指定路径），整体 9 秒兜底超时</summary>
    public static Node Verify(string host, string ip, string path, int timeoutMs)
    {
        Node[] box = new Node[1];
        if (!Task.Factory.StartNew(delegate
        {
            box[0] = VerifyInner(host, ip, path, timeoutMs);
        }).Wait(9000))
        {
            return new Node(host, ip)
            {
                State = "FAIL",
                Ms = 9000L
            };
        }
        if (box[0] == null)
        {
            return new Node(host, ip)
            {
                State = "FAIL"
            };
        }
        return box[0];
    }

    private static Node VerifyInner(string host, string ip, string path, int tcpTimeoutMs)
    {
        Node node = new Node(host, ip);
        Stopwatch sw = Stopwatch.StartNew();
        TcpClient client = null;
        SslStream ssl = null;
        StreamReader reader = null;
        try
        {
            client = new TcpClient();
            client.NoDelay = true;
            IAsyncResult ar = client.BeginConnect(ip, 443, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(tcpTimeoutMs))
            {
                node.State = "FAIL";
                node.Ms = sw.ElapsedMilliseconds;
                node.Code = 0;
                return node;
            }
            client.EndConnect(ar);
            client.ReceiveTimeout = 8000;
            client.SendTimeout = 8000;
            ssl = new SslStream(client.GetStream(), false);
            ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            if (path.Length == 0)
            {
                path = "/";
            }
            string request =
                "GET " + path + " HTTP/1.1\r\n" +
                "Host: " + host + "\r\n" +
                "User-Agent: " + Config.UA + "\r\n" +
                "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8\r\n" +
                "Accept-Language: zh-CN,zh;q=0.9,en;q=0.8\r\n" +
                "Connection: close\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(request);
            ssl.Write(bytes, 0, bytes.Length);
            ssl.Flush();

            reader = new StreamReader(ssl);
            Match match = Regex.Match(reader.ReadLine() ?? "", "HTTP/\\S+\\s+(\\d{3})");
            int code = node.Code = match.Success ? int.Parse(match.Groups[1].Value) : 0;
            node.Ms = sw.ElapsedMilliseconds;
            if (code >= 200 && code < 400)
            {
                node.State = "GOOD";
            }
            else if (code == 403 || code == 404)
            {
                node.State = "WEAK";
            }
            else
            {
                node.State = "BAD";
            }
            return node;
        }
        catch
        {
            node.State = "FAIL";
            node.Ms = sw.ElapsedMilliseconds;
            node.Code = 0;
            return node;
        }
        finally
        {
            if (reader != null)
            {
                try
                {
                    reader.Dispose();
                }
                catch
                {
                }
            }
            if (ssl != null)
            {
                try
                {
                    ssl.Dispose();
                }
                catch
                {
                }
            }
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>简单测下载速度（字节/秒），失败返回 -1</summary>
    public static long DownloadSpeed(string host, string ip, string path, int maxBytes)
    {
        TcpClient client = null;
        SslStream ssl = null;
        try
        {
            client = new TcpClient();
            IAsyncResult ar = client.BeginConnect(ip, 443, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(4000))
            {
                return -1L;
            }
            client.EndConnect(ar);
            ssl = new SslStream(client.GetStream(), false);
            ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            string request = "GET " + path + " HTTP/1.1\r\nHost: " + host + "\r\nUser-Agent: FastGithub\r\nConnection: close\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(request);
            ssl.Write(bytes, 0, bytes.Length);
            ssl.Flush();

            Stopwatch sw = Stopwatch.StartNew();
            int total = 0;
            byte[] buffer = new byte[8192];
            while (total < maxBytes)
            {
                int read = ssl.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }
                total += read;
                if (sw.ElapsedMilliseconds > 8000)
                {
                    break;
                }
            }
            long ms = sw.ElapsedMilliseconds;
            if (ms <= 0)
            {
                ms = 1L;
            }
            return (long)total * 1000L / ms;
        }
        catch
        {
            return -1L;
        }
        finally
        {
            if (ssl != null)
            {
                try
                {
                    ssl.Dispose();
                }
                catch
                {
                }
            }
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>镜像测速：返回 "状态码|耗时" 或 "ERR|原因|耗时"</summary>
    public static string ProbeMirror(string prefix, string sampleUrl)
    {
        Stopwatch sw = Stopwatch.StartNew();
        HttpClient client = null;
        try
        {
            client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(12.0);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Config.UA);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            return (int)client.GetAsync(MirrorStore.Normalize(prefix) + sampleUrl).Result.StatusCode
                   + "|" + sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            string message = ex.Message;
            if (message.Length > 60)
            {
                message = message.Substring(0, 60);
            }
            return "ERR|" + message + "|" + sw.ElapsedMilliseconds;
        }
        finally
        {
            if (client != null)
            {
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
