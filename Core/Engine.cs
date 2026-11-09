using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FastGithub;

public static class Engine
{
    /// <summary>单域名择优：候选池 + DoH → TCP 初筛 → TLS 复验</summary>
    public static Node ResolveOne(string host)
    {
        List<string> candidates = new List<string>();
        string[] pool = Config.Pool(host);
        for (int i = 0; i < pool.Length; i++)
        {
            candidates.Add(pool[i]);
        }

        string doh = Net.DoH(host);
        if (doh.Length > 0)
        {
            string[] parts = doh.Split(' ');
            for (int j = 0; j < parts.Length; j++)
            {
                string ip = parts[j].Trim();
                if (ip.Length > 6 && !candidates.Contains(ip))
                {
                    candidates.Add(ip);
                }
            }
        }

        // github.com 上的这两个 IP 已经被国内墙成黑洞，直接剔除
        if (host == "github.com")
        {
            candidates.RemoveAll(ip => ip == "20.205.243.166" || ip == "20.205.243.167");
        }

        string path = Config.ProbePath(host);

        List<KeyValuePair<string, long>> reachable = new List<KeyValuePair<string, long>>();
        for (int k = 0; k < candidates.Count; k++)
        {
            long ms = Net.TcpDelay(candidates[k], 1500);
            if (ms > 0)
            {
                reachable.Add(new KeyValuePair<string, long>(candidates[k], ms));
            }
        }
        reachable.Sort((a, b) => a.Value.CompareTo(b.Value));

        int probeCount = Math.Min(reachable.Count, 6);
        Node best = null;
        for (int m = 0; m < probeCount; m++)
        {
            Node node = Net.Verify(host, reachable[m].Key, path, 4000);
            // 网页型域名只要不是 200~399 就一律不采用，避免写进 hosts 后整站 404
            if (node.Acceptable)
            {
                if (best == null)
                {
                    best = node;
                }
                else if (node.Healthy && !best.Healthy)
                {
                    best = node;
                }
                else if (node.Healthy == best.Healthy && node.Ms < best.Ms)
                {
                    best = node;
                }
            }
            if (node.Healthy && node.Ms < 600)
            {
                break;
            }
        }

        if (best == null && reachable.Count > 0)
        {
            Node fallback = Net.Verify(host, reachable[0].Key, path, 5000);
            if (fallback.Acceptable)
            {
                return fallback;
            }
        }
        if (best == null)
        {
            return new Node(host, "")
            {
                State = "FAIL"
            };
        }
        return best;
    }

    /// <summary>并发探测全部域名，onNode 用于界面实时回填</summary>
    public static Dictionary<string, Node> ProbeAll(int degree, Action<Node> onNode)
    {
        string[] domains = Config.Domains;
        Dictionary<string, Node> map = new Dictionary<string, Node>();
        object lockObj = new object();
        Parallel.ForEach(domains, new ParallelOptions
        {
            MaxDegreeOfParallelism = degree
        }, delegate (string h)
        {
            Node node = ResolveOne(h);
            lock (lockObj)
            {
                map[h] = node;
            }
            if (onNode != null)
            {
                try
                {
                    onNode(node);
                }
                catch
                {
                }
            }
        });
        return map;
    }

    /// <summary>写 hosts 并缓存；可选的 log 用于把过程回显到界面</summary>
    public static bool Apply(Dictionary<string, Node> nodes, out int ok, out int conflicts, out int broken, StringBuilder log)
    {
        ok = 0;
        conflicts = 0;
        broken = 0;
        string text = Hosts.Read();
        if (string.IsNullOrEmpty(text))
        {
            log?.AppendLine("读取 hosts 失败（需要管理员权限）");
            return false;
        }

        Dictionary<string, string> map = new Dictionary<string, string>();
        for (int i = 0; i < Config.Domains.Length; i++)
        {
            string host = Config.Domains[i];
            if (!nodes.ContainsKey(host))
            {
                continue;
            }
            Node node = nodes[host];
            if (node.Acceptable && node.Ip != null && node.Ip.Length > 0)
            {
                map[host] = node.Ip;
                ok++;
            }
        }
        if (ok == 0)
        {
            log?.AppendLine("没有任何域名复验通过（网页型域名必须是 200~399），未修改 hosts");
            return false;
        }

        string clean = Hosts.StripBlock(text);
        List<string> found = Hosts.FindConflicts(clean);
        conflicts = found.Count;
        if (found.Count > 0)
        {
            log?.AppendLine("发现 " + found.Count + " 个被其它软件接管的域名，已临时接管（可完全还原）");
            clean = Hosts.CommentConflicts(clean);
        }

        string block = Hosts.BuildBlock(map);
        if (!Hosts.Write(clean.TrimEnd('\r', '\n') + "\r\n\r\n" + block))
        {
            log?.AppendLine("写入 hosts 失败");
            return false;
        }

        Dns.Flush();
        log?.AppendLine("已写入 " + ok + " 个域名，DNS 缓存已刷新");
        SaveCache(map);

        // 清完 DNS 缓存立刻复验一次：这一步能区分"我们的节点不行"和"本机上游 DNS 被劫持"。
        List<string> bad = HealthCheck(map);
        broken = bad.Count;
        if (bad.Count > 0)
        {
            log?.AppendLine("写入后复验：以下 " + bad.Count + " 个域名当前不可用 —— " + string.Join("、", bad.ToArray()));
            log?.AppendLine("刚清过 DNS 缓存仍打不开，多半是本机上游 DNS 被劫持或线路抖动。请检查路由器/宽带 DNS；其它网站若也异常，先点「停止并还原」对比。");
        }
        return true;
    }

    /// <summary>直接按 host→ip 映射重写 hosts（巡检换节点时用）</summary>
    public static bool ApplyMap(Dictionary<string, string> map, StringBuilder log)
    {
        if (map.Count == 0)
        {
            return false;
        }
        string text = Hosts.Read();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        string clean = Hosts.StripBlock(text);
        List<string> found = Hosts.FindConflicts(clean);
        if (found.Count > 0)
        {
            clean = Hosts.CommentConflicts(clean);
        }
        if (!Hosts.Write(clean.TrimEnd('\r', '\n') + "\r\n\r\n" + Hosts.BuildBlock(map)))
        {
            return false;
        }
        Dns.Flush();
        SaveCache(map);
        log?.AppendLine("已重写 hosts，接管 " + map.Count + " 个域名");
        return true;
    }

    /// <summary>
    /// 巡检当前已写入的节点是否还能用。返回失效的域名列表（空 = 一切正常）。
    /// 只查 CoreDomains 这 5 个：github.com 打不开最先能感觉到的就是它们。
    /// </summary>
    public static List<string> HealthCheck(Dictionary<string, string> applied)
    {
        List<string> bad = new List<string>();
        for (int i = 0; i < Config.CoreDomains.Length; i++)
        {
            string host = Config.CoreDomains[i];
            if (!applied.ContainsKey(host))
            {
                // 核心域名压根没写进 hosts，界面却显示"已加速"，同样是假状态
                bad.Add(host);
                continue;
            }
            string ip = applied[host];
            if (string.IsNullOrEmpty(ip))
            {
                bad.Add(host);
                continue;
            }
            Node node = Net.Verify(host, ip, Config.ProbePath(host), 3000);
            if (!node.Acceptable)
            {
                bad.Add(host);
            }
        }
        return bad;
    }

    /// <summary>
    /// 给失效域名重新择优并重写 hosts。返回成功换掉的个数。
    /// </summary>
    public static int Repair(Dictionary<string, string> applied, List<string> broken, StringBuilder log)
    {
        int fixedCount = 0;
        for (int i = 0; i < broken.Count; i++)
        {
            string host = broken[i];
            Node node = ResolveOne(host);
            if (node.Acceptable && !string.IsNullOrEmpty(node.Ip))
            {
                applied[host] = node.Ip;
                fixedCount++;
                log?.AppendLine("「" + host + "」节点失效，已自动切换到 " + node.Ip + "（" + node.Ms + " ms）");
            }
            else
            {
                log?.AppendLine("「" + host + "」节点失效，暂时没有可用替代，下次巡检继续尝试");
            }
        }
        if (fixedCount > 0)
        {
            ApplyMap(applied, log);
        }
        return fixedCount;
    }

    /// <summary>还原：删加速块 + 去掉 "#FG# " 前缀</summary>
    public static bool Restore(StringBuilder log)
    {
        string text = Hosts.Read();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        if (!Hosts.Write(Hosts.RestoreOverrides(Hosts.StripBlock(text)).TrimEnd('\r', '\n') + "\r\n"))
        {
            return false;
        }
        Dns.Flush();
        log?.AppendLine("hosts 已还原");
        return true;
    }

    public static string CachePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastGithub");
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
        }
        return Path.Combine(dir, "nodes.txt");
    }

    public static void SaveCache(Dictionary<string, string> map)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> item in map)
            {
                sb.Append(item.Key).Append('\t').Append(item.Value).Append("\r\n");
            }
            File.WriteAllText(CachePath(), sb.ToString(), new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    public static Dictionary<string, string> LoadCache()
    {
        Dictionary<string, string> map = new Dictionary<string, string>();
        try
        {
            string path = CachePath();
            if (!File.Exists(path))
            {
                return map;
            }
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length != 0)
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        map[parts[0]] = parts[1];
                    }
                }
            }
        }
        catch
        {
        }
        return map;
    }

    public static void ClearCache()
    {
        try
        {
            string path = CachePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    /// <summary>hosts 被第三方软件清掉后，按缓存自动补回</summary>
    public static bool ReapplyFromCache(out int count)
    {
        count = 0;
        Dictionary<string, string> cache = LoadCache();
        if (cache.Count == 0)
        {
            return false;
        }
        string text = Hosts.Read();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        string clean = Hosts.StripBlock(text);
        if (!Hosts.Write(clean.TrimEnd('\r', '\n') + "\r\n\r\n" + Hosts.BuildBlock(cache)))
        {
            return false;
        }
        Dns.Flush();
        count = cache.Count;
        return true;
    }
}
