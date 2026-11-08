namespace FastGithub;

public class Node
{
    public string Host { get; set; }

    public string Ip { get; set; }

    public long Ms { get; set; }

    public int Code { get; set; }

    public string State { get; set; }

    /// <summary>GOOD / WEAK 视为可用</summary>
    public bool Usable => State == "GOOD" || State == "WEAK";

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
