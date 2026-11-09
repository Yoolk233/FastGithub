using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace FastGithub;

public partial class MainWindow : Window
{
    private const string DefaultAccent = "#FF0F6CBD";

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

    private Grid mainPage;
    private Grid settingsPage;
    private Button navMainBtn;
    private Button navSettingsBtn;
    private Button backBtn;
    private Button resetAccentBtn;
    private Button accentPreview;
    private TextBlock accentLabel;
    private readonly List<Button> swatches = new List<Button>();

    private DispatcherTimer watchdog;
    private bool busy;
    private bool healthBusy;
    private bool degraded;
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

        mainPage = this.FindControl<Grid>("MainPage");
        settingsPage = this.FindControl<Grid>("SettingsPage");
        navMainBtn = this.FindControl<Button>("NavMainBtn");
        navSettingsBtn = this.FindControl<Button>("NavSettingsBtn");
        backBtn = this.FindControl<Button>("BackBtn");
        resetAccentBtn = this.FindControl<Button>("ResetAccentBtn");
        accentPreview = this.FindControl<Button>("AccentPreview");
        accentLabel = this.FindControl<TextBlock>("AccentLabel");
        for (int i = 0; i < 6; i++)
        {
            swatches.Add(this.FindControl<Button>("Swatch" + i));
        }

        nodeGrid.ItemsSource = rows;
        for (int i = 0; i < Config.Domains.Length; i++)
        {
            rows.Add(new Node(Config.Domains[i], ""));
        }

        startBtn.Click += async (_, _) => await ProbeAndApply(false);
        refreshBtn.Click += async (_, _) => await ProbeAndApply(true);
        stopBtn.Click += async (_, _) => await StopAccel();
        linkBtn.Click += async (_, _) => await new MirrorWindow().ShowDialog(this);

        navMainBtn.Click += (_, _) => ShowPage(true);
        navSettingsBtn.Click += (_, _) => ShowPage(false);
        backBtn.Click += (_, _) => ShowPage(true);
        resetAccentBtn.Click += (_, _) => ApplyAccent(DefaultAccent, true);
        for (int i = 0; i < swatches.Count; i++)
        {
            Button swatch = swatches[i];
            swatch.Click += (_, _) => ApplyAccent(swatch.Tag as string, true);
        }

        ShowPage(true);
        ApplyAccent(LoadAccent(), false);

        Loaded += OnLoaded;
        Closed += (_, _) => watchdog?.Stop();
    }

    private void OnLoaded(object sender, EventArgs e)
    {
        Log("FastGithub " + Config.Version + " 已启动：网页走 hosts 直连，下载走镜像加速。");
        Log("已启用节点巡检：每 45 秒复验一次，节点失效自动换 IP。");
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
                if (value.Acceptable)
                {
                    usable++;
                }
            }
            Log("测速完成：可写入 " + usable + " / " + map.Count + " 个域名（网页型域名只认 200~399）。");
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
            degraded = false;
            Log("加速已生效，接管 " + ok + " 个域名。浏览器可直接打开 github.com");
            StartWatchdog();
        }
        else
        {
            Log("本次没有写入 hosts，请检查权限或网络。");
        }
    }

    private Task StopAccel()
    {
        if (busy)
        {
            return Task.CompletedTask;
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
        degraded = false;
        busy = false;
        SetButtonsEnabled(true);
        RefreshState();
        if (result)
        {
            Log("已还原：hosts 恢复原样，GitHub 走默认解析。");
        }
        return Task.CompletedTask;
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
            if (degraded)
            {
                stateText.Text = "加速已失效";
                stateText.Foreground = Hex("#C42B1C");
                chipText.Text = "● 加速已失效";
                chipText.Foreground = Hex("#C42B1C");
                detailText.Text = "当前节点已不可用，正在自动重新择优换节点…";
                statusText.Text = "已失效 · 自动换节点中";
            }
            else
            {
                stateText.Text = "加速已生效";
                stateText.Foreground = Hex("#0F7B0F");
                chipText.Text = "● 加速已生效";
                chipText.Foreground = Hex("#0F7B0F");
                detailText.Text = "hosts 已接管 " + applied.Count + " 个域名 · 浏览器直接访问 github.com";
                statusText.Text = "加速中 · 接管 " + applied.Count + " 个域名";
            }
        }
        else
        {
            applied = new Dictionary<string, string>();
            degraded = false;
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

    private void ShowPage(bool showMain)
    {
        mainPage.IsVisible = showMain;
        settingsPage.IsVisible = !showMain;
        SetNavActive(navMainBtn, showMain);
        SetNavActive(navSettingsBtn, !showMain);
    }

    private static void SetNavActive(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("active"))
            {
                button.Classes.Add("active");
            }
        }
        else
        {
            button.Classes.Remove("active");
        }
    }

    private void ApplyAccent(string hex, bool persist)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return;
        }
        Color accent;
        try
        {
            accent = Color.Parse(hex);
        }
        catch
        {
            return;
        }

        SolidColorBrush brush = new SolidColorBrush(accent);
        SolidColorBrush hover = new SolidColorBrush(Shade(accent, 0.16));
        SolidColorBrush press = new SolidColorBrush(Shade(accent, 0.36));
        SolidColorBrush soft = new SolidColorBrush(Tint(accent, 0.76));

        // 窗口级资源：命中本窗口内所有 DynamicResource 查找，替换即刻重绘
        this.Resources["AccentBrush"] = brush;
        this.Resources["AccentHoverBrush"] = hover;
        this.Resources["AccentPressBrush"] = press;
        this.Resources["AccentSoftBrush"] = soft;

        // 应用级资源：让其它窗口（下载加速窗）也用同一强调色
        Application app = Application.Current;
        if (app != null)
        {
            app.Resources["AccentBrush"] = brush;
            app.Resources["AccentHoverBrush"] = hover;
            app.Resources["AccentPressBrush"] = press;
            app.Resources["AccentSoftBrush"] = soft;
        }

        if (accentLabel != null)
        {
            accentLabel.Text = "当前强调色：" + AccentName(hex);
        }

        if (persist)
        {
            SaveAccent(hex);
            Log("强调色已切换为 " + AccentName(hex) + "（" + hex + "）。");
        }
    }

    private static string AccentName(string hex)
    {
        switch (hex.ToUpperInvariant())
        {
            case "#FF0F6CBD":
                return "蓝";
            case "#FF0E7C7B":
                return "青";
            case "#FF107C10":
                return "绿";
            case "#FFC05621":
                return "橙";
            case "#FF6B4FA1":
                return "紫";
            case "#FFC42B1C":
                return "红";
            default:
                return "自定义";
        }
    }

    private static Color Shade(Color color, double factor)
    {
        return Color.FromArgb(255,
            (byte)(color.R * (1.0 - factor)),
            (byte)(color.G * (1.0 - factor)),
            (byte)(color.B * (1.0 - factor)));
    }

    private static Color Tint(Color color, double factor)
    {
        return Color.FromArgb(255,
            (byte)(color.R + (255 - color.R) * factor),
            (byte)(color.G + (255 - color.G) * factor),
            (byte)(color.B + (255 - color.B) * factor));
    }

    private static string UiPath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastGithub");
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
        }
        return Path.Combine(dir, "ui.txt");
    }

    private static string LoadAccent()
    {
        try
        {
            string path = UiPath();
            if (!File.Exists(path))
            {
                return DefaultAccent;
            }
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("accent="))
                {
                    string value = line.Substring(7).Trim();
                    if (value.Length > 0)
                    {
                        return value;
                    }
                }
            }
        }
        catch
        {
        }
        return DefaultAccent;
    }

    private static void SaveAccent(string hex)
    {
        try
        {
            File.WriteAllText(UiPath(), "accent=" + hex + "\r\n", new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    private void StartWatchdog()
    {
        if (watchdog == null)
        {
            watchdog = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(45.0)
            };
            watchdog.Tick += (_, _) => TickWatchdog();
        }
        if (!watchdog.IsEnabled)
        {
            watchdog.Start();
        }
    }

    /// <summary>
    /// 巡检：hosts 块被清掉就补回；块还在但节点已失效（典型表现：网页 404 / 超时 / 突然断连
    /// 而界面还写着"已加速"）就重新择优换 IP，并把界面状态同步成真实状态。
    /// </summary>
    private async void TickWatchdog()
    {
        if (busy || healthBusy)
        {
            return;
        }
        healthBusy = true;
        try
        {
            string text = Hosts.Read();
            if (!Hosts.HasBlock(text))
            {
                int count = 0;
                if (Engine.ReapplyFromCache(out count) && count > 0)
                {
                    Log("hosts 被其它软件改写了，已自动补回 " + count + " 个域名。");
                    degraded = false;
                    RefreshState();
                    SetButtonsEnabled(!busy);
                }
                return;
            }

            Dictionary<string, string> current = Hosts.ParseBlock(text);
            List<string> bad = await Task.Run(() => Engine.HealthCheck(current));
            if (bad.Count == 0)
            {
                if (degraded)
                {
                    degraded = false;
                    RefreshState();
                    Log("巡检恢复正常，当前节点均可用。");
                }
                return;
            }

            StringBuilder sb = new StringBuilder();
            int repaired = await Task.Run(() => Engine.Repair(current, bad, sb));
            foreach (string line in sb.ToString().Split('\n'))
            {
                if (line.Trim().Length > 0)
                {
                    Log(line.Trim());
                }
            }

            if (repaired > 0)
            {
                degraded = false;
                applied = current;
                Log("巡检发现 " + bad.Count + " 个域名节点失效，已自动切换到可用 IP 并重写 hosts。");
                RefreshState();
                SetButtonsEnabled(!busy);
            }
            else
            {
                degraded = true;
                Log("巡检发现 " + bad.Count + " 个域名当前不可用，45 秒后继续自动重试。");
                RefreshState();
            }
        }
        catch
        {
        }
        finally
        {
            healthBusy = false;
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
