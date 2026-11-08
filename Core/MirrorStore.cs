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
        return "https://ghfast.top/";
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
            return "https://ghfast.top/";
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
