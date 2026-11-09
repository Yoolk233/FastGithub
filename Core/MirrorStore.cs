using System;
using System.IO;
using System.Text;

namespace FastGithub;

public static class MirrorStore
{
    private static string Dir()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastGithub");
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
        }
        return dir;
    }

    public static string SavePath()
    {
        return Path.Combine(Dir(), "mirror.txt");
    }

    public static string Load()
    {
        try
        {
            string path = SavePath();
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path).Trim();
                if (text.Length > 0)
                {
                    return text;
                }
            }
        }
        catch
        {
        }
        // 1.2.2 起不再内置任何第三方镜像域名：留空＝不使用镜像
        return Config.Mirror;
    }

    public static void Save(string prefix)
    {
        try
        {
            File.WriteAllText(SavePath(), prefix.Trim(), new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    public static string Normalize(string prefix)
    {
        string text = prefix == null ? "" : prefix.Trim();
        if (text.Length == 0)
        {
            // 空前缀＝不使用镜像，把原始 GitHub 链接原样给用户
            return "";
        }
        if (!text.EndsWith("/"))
        {
            text += "/";
        }
        return text;
    }

    /// <summary>把原始 GitHub 链接套上镜像前缀（已经是镜像链接则先剥掉旧前缀）</summary>
    public static string Apply(string prefix, string url)
    {
        string text = url == null ? "" : url.Trim();
        if (text.Length == 0)
        {
            return "";
        }
        string normalized = Normalize(prefix);
        string[] presets = Config.MirrorPresets;
        for (int i = 0; i < presets.Length; i++)
        {
            if (text.StartsWith(presets[i], StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(presets[i].Length);
                break;
            }
        }
        if (normalized.Length == 0)
        {
            // 没配镜像：直接返回原始链接，绝不套任何第三方域名
            return text;
        }
        return normalized + text;
    }

    /// <summary>剥掉已知镜像前缀，还原成原始链接</summary>
    public static string Strip(string url)
    {
        string text = url == null ? "" : url.Trim();
        string[] presets = Config.MirrorPresets;
        for (int i = 0; i < presets.Length; i++)
        {
            if (text.StartsWith(presets[i], StringComparison.OrdinalIgnoreCase))
            {
                return text.Substring(presets[i].Length);
            }
        }
        return text;
    }
}
