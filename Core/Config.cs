namespace FastGithub;

public static class Config
{
    public const string AppName = "FastGithub";

    /// <summary>显示用版本号</summary>
    public const string Version = "1.2.2Criticalissuefix";

    /// <summary>hosts 加速块的起始标记</summary>
    public const string MarkBegin = "# ==== FastGithub BEGIN (auto generated) ====";

    /// <summary>hosts 加速块的结束标记</summary>
    public const string MarkEnd = "# ==== FastGithub END ====";

    /// <summary>被其它软件占用的行，临时加这个前缀注释掉，还原时去掉</summary>
    public const string OvPrefix = "#FG# ";

    public const string HostsPath = @"C:\Windows\System32\drivers\etc\hosts";

    /// <summary>
    /// 默认镜像前缀。
    /// 内置的第三方 ghproxy 域名在 1.2.2 已全部移除：实测 ghfast.top / ghproxy.net /
    /// gh-proxy.com / mirror.ghproxy.com / gh.llkk.cc 全部超时或 502，ghp.ci 域名直接解析失败。
    /// 这类死链在浏览器里点开就是广告页甚至博彩跳转页，不能再随程序分发。
    /// 现在只由用户自己填写自己的镜像前缀，留空＝不使用镜像。
    /// </summary>
    public const string Mirror = "";

    public const string UA =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/131.0.0.0 Safari/537.36";

    /// <summary>
    /// 下载加速的预设镜像前缀：故意留空。
    /// 见上面 Mirror 的说明——内置第三方镜像域名会把用户带到不干净的站点，
    /// 需要镜像请用户自己填一个可信前缀（程序会先测速再允许使用）。
    /// </summary>
    public static readonly string[] MirrorPresets = new string[0];

    /// <summary>接管的 GitHub 域名</summary>
    public static readonly string[] Domains =
    {
        "github.com",
        "api.github.com",
        "raw.githubusercontent.com",
        "gist.github.com",
        "gist.githubusercontent.com",
        "codeload.github.com",
        "objects.githubusercontent.com",
        "avatars.githubusercontent.com",
        "avatars0.githubusercontent.com",
        "avatars1.githubusercontent.com",
        "camo.githubusercontent.com",
        "user-images.githubusercontent.com",
        "private-user-images.githubusercontent.com",
        "cloud.githubusercontent.com",
        "media.githubusercontent.com",
        "raw.github.com",
        "github.githubassets.com",
        "github.io",
        "pages.github.com",
        "education.github.com",
        "uploads.github.com"
    };

    /// <summary>
    /// 网页型域名：hosts 里写错一个 IP，浏览器就是整站 404 / 打不开。
    /// 这些域名必须复验出 200~399 才能写入，只“能连上”不算数。
    /// </summary>
    public static bool IsStrict(string host)
    {
        return host == "github.com"
            || host == "api.github.com"
            || host == "gist.github.com"
            || host == "github.io";
    }

    /// <summary>复验用的路径：github.com 用 /login（真实页面），其余用根路径</summary>
    public static string ProbePath(string host)
        => host == "github.com" ? "/login" : "/";

    /// <summary>巡检只看这几个核心域名，保证 45 秒一轮不会太慢</summary>
    public static readonly string[] CoreDomains =
    {
        "github.com",
        "api.github.com",
        "codeload.github.com",
        "raw.githubusercontent.com",
        "github.githubassets.com"
    };

    /// <summary>每个域名的候选 IP 池（会再和 DoH 解析结果合并）</summary>
    public static string[] Pool(string host)
    {
        switch (host)
        {
            case "github.com":
                return new[]
                {
                    "20.27.177.113", "20.27.177.115", "20.27.177.116", "20.233.83.145",
                    "140.82.112.3", "140.82.113.3", "140.82.114.3", "4.237.22.38",
                    "20.200.245.247", "20.207.73.82", "20.26.156.215"
                };
            case "api.github.com":
                return new[] { "20.205.243.168", "20.27.177.116", "140.82.112.6", "140.82.113.6", "140.82.114.6", "140.82.116.6" };
            case "codeload.github.com":
                return new[] { "20.205.243.165", "20.27.177.115", "140.82.112.9", "140.82.113.9", "140.82.114.9" };
            case "gist.github.com":
                return new[] { "20.205.243.166", "20.205.243.167", "20.27.177.113", "140.82.112.3", "140.82.113.3", "140.82.114.3", "20.200.245.247" };
            case "education.github.com":
                return new[] { "140.82.114.22", "140.82.113.21" };
            case "uploads.github.com":
                return new[] { "20.205.243.161" };
            case "github.githubassets.com":
                return new[] { "185.199.108.215", "185.199.109.215", "185.199.110.215", "185.199.111.215" };
            case "github.io":
            case "pages.github.com":
                return new[] { "185.199.108.153", "185.199.109.153", "185.199.110.153", "185.199.111.153" };
            default:
                return new[] { "185.199.108.133", "185.199.109.133", "185.199.110.133", "185.199.111.133" };
        }
    }
}
