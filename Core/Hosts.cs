using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FastGithub;

public static class Hosts
{
    private const string HostsFile = @"C:\Windows\System32\drivers\etc\hosts";
    private const string BackupPath = @"C:\Windows\System32\drivers\etc\hosts.fastgithub.bak";

    /// <summary>读 hosts，先按 UTF-8 严格解码，失败再用系统默认编码</summary>
    public static string Read()
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(HostsFile);
            bool isUtf8 = true;
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
            }
            catch
            {
                isUtf8 = false;
            }
            return isUtf8
                ? new UTF8Encoding(false).GetString(bytes)
                : Encoding.Default.GetString(bytes);
        }
        catch (Exception)
        {
            return "";
        }
    }

    /// <summary>写 hosts：第一次写之前备份原文件，之后不覆盖备份</summary>
    public static bool Write(string text)
    {
        try
        {
            if (!File.Exists(BackupPath))
            {
                File.Copy(HostsFile, BackupPath, false);
            }
            File.WriteAllText(HostsFile, text, new UTF8Encoding(false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool HasBlock(string text)
    {
        return text.IndexOf(Config.MarkBegin, StringComparison.Ordinal) >= 0;
    }

    /// <summary>删掉加速块，其余内容原样保留</summary>
    public static string StripBlock(string text)
    {
        StringBuilder sb = new StringBuilder();
        bool inside = false;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (string line in lines)
        {
            if (line.Trim() == Config.MarkBegin)
            {
                inside = true;
            }
            else if (line.Trim() == Config.MarkEnd)
            {
                inside = false;
            }
            else if (!inside)
            {
                sb.Append(line).Append("\r\n");
            }
        }
        return sb.ToString();
    }

    /// <summary>把 "#FG# " 开头的行还原</summary>
    public static string RestoreOverrides(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        StringBuilder sb = new StringBuilder();
        foreach (string line in lines)
        {
            if (line.StartsWith(Config.OvPrefix, StringComparison.Ordinal))
            {
                sb.Append(line.Substring(Config.OvPrefix.Length));
            }
            else
            {
                sb.Append(line);
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>找出被其它软件（Steam++ 之类）指向的 GitHub 域名</summary>
    public static List<string> FindConflicts(string text)
    {
        List<string> found = new List<string>();
        HashSet<string> watch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Config.Domains.Length; i++)
        {
            watch.Add(Config.Domains[i]);
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }
            string[] parts = Regex.Split(line, "\\s+");
            if (parts.Length < 2)
            {
                continue;
            }
            for (int j = 1; j < parts.Length; j++)
            {
                string host = parts[j].Trim().ToLowerInvariant();
                if (host.StartsWith("www."))
                {
                    host = host.Substring(4);
                }
                if (watch.Contains(host) && !found.Contains(host))
                {
                    found.Add(host);
                }
            }
        }
        return found;
    }

    /// <summary>
    /// 找出 hosts 里被别的加速器改成本机回环的域名（127.0.0.1 / ::1 / localhost）。
    /// 这类条目是 Watt Toolkit、机场客户端之类"本地代理"留下的：那个软件一退出，
    /// 对应域名就整体打不开，用户却会以为是本工具的问题，所以要显式提示。
    /// </summary>
    public static List<string> FindLocalProxy(string text)
    {
        List<string> found = new List<string>();
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }
            string[] parts = Regex.Split(line, "\\s+");
            if (parts.Length < 2)
            {
                continue;
            }
            string address = parts[0].Trim();
            bool loopback = address == "127.0.0.1" || address == "::1" || address == "0.0.0.0"
                            || string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase);
            if (!loopback)
            {
                continue;
            }
            for (int j = 1; j < parts.Length; j++)
            {
                string host = parts[j].Trim().ToLowerInvariant();
                if (host.Length == 0 || found.Contains(host))
                {
                    continue;
                }
                found.Add(host);
            }
        }
        return found;
    }

    /// <summary>把冲突行整体注释成 "#FG# ..."，还原时去掉前缀</summary>
    public static string CommentConflicts(string text)
    {
        List<string> conflicts = FindConflicts(text);
        if (conflicts.Count == 0)
        {
            return text;
        }
        HashSet<string> watch = new HashSet<string>(conflicts, StringComparer.OrdinalIgnoreCase);
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        StringBuilder sb = new StringBuilder();
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            bool hit = false;
            if (trimmed.Length > 0 && !trimmed.StartsWith("#"))
            {
                string[] parts = Regex.Split(trimmed, "\\s+");
                if (parts.Length >= 2)
                {
                    for (int j = 1; j < parts.Length; j++)
                    {
                        string host = parts[j].Trim().ToLowerInvariant();
                        if (host.StartsWith("www."))
                        {
                            host = host.Substring(4);
                        }
                        if (watch.Contains(host))
                        {
                            hit = true;
                            break;
                        }
                    }
                }
            }
            sb.Append(hit ? (Config.OvPrefix + line) : line).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>生成 hosts 加速块</summary>
    public static string BuildBlock(Dictionary<string, string> map)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Config.MarkBegin).Append("\r\n");
        sb.Append("# 由 FastGithub 生成，删除本块或点击「停止并还原」即可恢复\r\n");
        string[] domains = Config.Domains;
        for (int i = 0; i < domains.Length; i++)
        {
            if (map.ContainsKey(domains[i]))
            {
                sb.Append(map[domains[i]]).Append('\t').Append(domains[i]).Append("\r\n");
            }
        }
        sb.Append(Config.MarkEnd).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>解析 hosts 里已有的加速块，重启后回显状态用</summary>
    public static Dictionary<string, string> ParseBlock(string text)
    {
        Dictionary<string, string> map = new Dictionary<string, string>();
        bool inside = false;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line == Config.MarkBegin)
            {
                inside = true;
            }
            else if (line == Config.MarkEnd)
            {
                inside = false;
            }
            else if (inside && line.Length != 0 && !line.StartsWith("#"))
            {
                string[] parts = Regex.Split(line, "\\s+");
                if (parts.Length >= 2)
                {
                    map[parts[1]] = parts[0];
                }
            }
        }
        return map;
    }
}
