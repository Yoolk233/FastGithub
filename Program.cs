using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;

namespace FastGithub;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ThreadPool.SetMinThreads(64, 64);
        }
        catch
        {
        }
        try
        {
            ServicePointManager.DefaultConnectionLimit = 200;
        }
        catch
        {
        }
        try
        {
            ServicePointManager.Expect100Continue = false;
        }
        catch
        {
        }

        if (args != null && args.Length != 0)
        {
            if (args[0] == "--selftest")
            {
                SelfTest(args);
                return;
            }
            if (args[0] == "--apply")
            {
                CliApply();
                return;
            }
            if (args[0] == "--restore")
            {
                CliRestore();
                return;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace(LogEventLevel.Warning);
    }

    private static void SelfTest(string[] args)
    {
        string path = Path.Combine(Path.GetTempPath(), "fastgithub_selftest.txt");
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--out="))
            {
                path = args[i].Substring(6);
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("FastGithub 1.2.0 selftest");
        sb.AppendLine("--- hosts 状态 ---");
        string hosts = Hosts.Read();
        sb.AppendLine("hasBlock=" + Hosts.HasBlock(hosts));

        string[] domains = Config.Domains;
        List<string> lines = new List<string>();
        object lk = new object();
        Parallel.ForEach(domains, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4
        }, delegate (string hh)
        {
            Node node = Engine.ResolveOne(hh);
            lock (lk)
            {
                lines.Add($"{hh,-40} {node.Ip,-16} {node.Ms,6}ms  {node.State,-5} code={node.Code}");
            }
        });
        sb.AppendLine(string.Join("\r\n", lines.ToArray()));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static void CliApply()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("FastGithub CLI apply " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Dictionary<string, Node> map = Engine.ProbeAll(4, null);
        int ok = 0;
        int conflicts = 0;
        bool result = Engine.Apply(map, out ok, out conflicts, sb);
        sb.AppendLine("result=" + result + "  ok=" + ok + "  conflicts=" + conflicts);
        for (int i = 0; i < Config.Domains.Length; i++)
        {
            string host = Config.Domains[i];
            if (map.ContainsKey(host))
            {
                Node node = map[host];
                sb.AppendLine($"  {host,-40} {node.Ip,-16} {node.Ms,6}ms  {node.State,-5} code={node.Code}");
            }
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "fastgithub_cli.txt"), sb.ToString(), new UTF8Encoding(false));
    }

    private static void CliRestore()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("FastGithub CLI restore " + DateTime.Now.ToString("HH:mm:ss"));
        sb.AppendLine("result=" + Engine.Restore(sb));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "fastgithub_cli.txt"), sb.ToString(), new UTF8Encoding(false));
    }
}
