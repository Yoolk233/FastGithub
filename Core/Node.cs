namespace FastGithub;

public class Node
{
    public string Host { get; set; }

    public string Ip { get; set; }

    public long Ms { get; set; }

    public int Code { get; set; }

    public string State { get; set; }

    /// <summary>
    /// 可写入 hosts：GOOD 表示真的返回了页面/接口；
    /// WEAK（403/404）只允许用于资源类域名，网页型域名由 Config.IsStrict 另行把关。
    /// </summary>
    public bool Usable => State == "GOOD" || State == "WEAK";

    /// <summary>真正返回了 200~399</summary>
    public bool Healthy => State == "GOOD";

    /// <summary>
    /// 该节点能否写进 hosts：网页型域名必须 HEALTHY，资源类域名 GOOD / WEAK 都行。
    /// </summary>
    public bool Acceptable => Config.IsStrict(Host) ? Healthy : Usable;

    public string MsText => Ms <= 0 ? "-" : Ms + " ms";

    public string StateText => State switch
    {
        "GOOD" => "可用 · " + Ms + " ms",
        "WEAK" => "受限 · " + Ms + " ms",
        "BAD" => "不可用 · " + Code,
        "FAIL" => "失败",
        _ => "待测速"
    };

    public Node()
    {
        State = "--";
    }

    public Node(string h, string ip)
    {
        Host = h;
        Ip = ip;
        State = "--";
        Ms = 0;
        Code = 0;
    }
}
