using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace FastGithub;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Node> rows = new ObservableCollection<Node>();

    private Button startBtn;
    private Button stopBtn;
    private Button refreshBtn;
    private Button linkBtn;
    private TextBlock chipText;
    private TextBlock stateText;
    private TextBlock detailText;
    private TextBlock statusText;
    private TextBox logBox;
    private DataGrid nodeGrid;

    private DispatcherTimer watchdog;
    private bool busy;
    private Dictionary<string, string> applied = new Dictionary<string, string>();

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        startBtn = this.FindControl<Button>("StartBtn");
        stopBtn = this.FindControl<Button>("StopBtn");
        refreshBtn = this.FindControl<Button>("RefreshBtn");
        linkBtn = this.FindControl<Button>("LinkBtn");
        chipText = this.FindControl<TextBlock>("ChipText");
        stateText = this.FindControl<TextBlock>("StateText");
        detailText = this.FindControl<TextBlock>("DetailText");
        statusText = this.FindControl<TextBlock>("StatusText");
        logBox = this.FindControl<TextBox>("LogBox");
        nodeGrid = this.FindControl<DataGrid>("NodeGrid");

        nodeGrid.ItemsSource = rows;
        for (int i = 0; i < Config.Domains.Length; i++)
        {
            rows.Add(new Node(Config.Domains[i], ""));
        }

        startBtn.Click += async (_, _) => await ProbeAndApply(false);
        refreshBtn.Click += async (_, _) => await ProbeAndApply(true);
        stopBtn.Click += async (_, _) => await StopAccel();
        linkBtn.Click += async (_, _) => await new MirrorWindow().ShowDialog(this);

        Loaded += OnLoaded;
        Closed += (_, _) => watchdog?.Stop();
    }

    private void OnLoaded(object sender, EventArgs e)
    {
        Log("FastGithub 1.2.0 已启动：网页走 hosts 直连，下载走镜像加速。");
        if (!Hosts.HasBlock(Hosts.Read()))
        {
            int count = 0;
            if (Engine.ReapplyFromCache(out count) && count > 0)
            {
                Log("上次的加速配置被清除了，已按缓存自动恢复 " + count + " 个域名。");
            }
        }
        RefreshState();
        if (Engine.LoadCache().Count > 0 || Hosts.HasBlock(Hosts.Read()))
        {
            StartWatchdog();
        }
    }

    private async Task ProbeAndApply(bool probeOnly)
    {
        if (busy)
        {
            return;
        }
        busy = true;
        SetButtonsEnabled(false);
        Log(probeOnly ? "开始重新测速…" : ("开始测速并择优：共 " + Config.Domains.Length + " 个域名…"));

        Dictionary<string, Node> map = await Task.Run(() => Engine.ProbeAll(4, delegate (Node n)
        {
            Dispatcher.UIThread.Post(() => MergeRow(n));
        }));

        if (probeOnly)
        {
            int usable = 0;
            foreach (Node value in map.Values)
            {
                if (value.Usable)
                {
                    usable++;
                }
            }
            Log("测速完成：可用 " + usable + " / " + map.Count + " 个域名。");
            busy = false;
            SetButtonsEnabled(true);
            RefreshState();
            return;
        }

        StringBuilder sb = new StringBuilder();
        int ok = 0;
        int conflicts = 0;
        bool result = Engine.Apply(map, out ok, out conflicts, sb);
        foreach (string line in sb.ToString().Split('\n'))
        {
            if (line.Trim().Length > 0)
            {
                Log(line.Trim());
            }
        }
        busy = false;
        SetButtonsEnabled(true);
        RefreshState();
        if (result)
        {
            Log("加速已生效，接管 " + ok + " 个域名。浏览器可直接打开 github.com");
            StartWatchdog();
        }
        else
        {
            Log("本次没有写入 hosts，请检查权限或网络。");
        }
    }

    private async Task StopAccel()
    {
        if (busy)
        {
            return;
        }
        busy = true;
        SetButtonsEnabled(false);
        StringBuilder sb = new StringBuilder();
        bool result = Engine.Restore(sb);
        foreach (string line in sb.ToString().Split('\n'))
        {
            if (line.Trim().Length > 0)
            {
                Log(line.Trim());
            }
        }
        busy = false;
        SetButtonsEnabled(true);
        RefreshState();
        if (result)
        {
            Log("已还原：hosts 恢复原样，GitHub 走默认解析。");
        }
    }

    private void MergeRow(Node n)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].Host, n.Host, StringComparison.OrdinalIgnoreCase))
            {
                rows[i] = n;
                break;
            }
        }
    }

    private void RefreshState()
    {
        string text = Hosts.Read();
        if (Hosts.HasBlock(text))
        {
            applied = Hosts.ParseBlock(text);
            stateText.Text = "加速已生效";
            stateText.Foreground = Hex("#0F7B0F");
            chipText.Text = "● 加速已生效";
            chipText.Foreground = Hex("#0F7B0F");
            detailText.Text = "hosts 已接管 " + applied.Count + " 个域名 · 浏览器直接访问 github.com";
            statusText.Text = "加速中 · 接管 " + applied.Count + " 个域名";
        }
        else
        {
            applied = new Dictionary<string, string>();
            stateText.Text = "未加速";
            stateText.Foreground = Hex("#616161");
            chipText.Text = "● 未加速";
            chipText.Foreground = Hex("#8A8A8A");
            detailText.Text = "点右侧「一键加速」，自动测速并写入优选节点";
            statusText.Text = "就绪 · 一个 exe，双击即用";
        }
    }

    private void SetButtonsEnabled(bool on)
    {
        startBtn.IsEnabled = on;
        refreshBtn.IsEnabled = on;
        stopBtn.IsEnabled = on && Hosts.HasBlock(Hosts.Read());
    }

    private void StartWatchdog()
    {
        if (watchdog == null)
        {
            watchdog = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60.0)
            };
            watchdog.Tick += (_, _) => TickWatchdog();
        }
        if (!watchdog.IsEnabled)
        {
            watchdog.Start();
        }
    }

    private void TickWatchdog()
    {
        if (!Hosts.HasBlock(Hosts.Read()))
        {
            int count = 0;
            if (Engine.ReapplyFromCache(out count) && count > 0)
            {
                Log("hosts 被其它软件改写了，已自动补回 " + count + " 个域名。");
                RefreshState();
                SetButtonsEnabled(!busy);
            }
        }
    }

    private void Log(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
        logBox.Text = string.IsNullOrEmpty(logBox.Text) ? line : (logBox.Text + "\n" + line);
        logBox.CaretIndex = logBox.Text.Length;
    }

    private static IBrush Hex(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }
}
