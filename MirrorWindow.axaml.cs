using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace FastGithub;

public partial class MirrorWindow : Window
{
    private const string SampleUrl = "https://github.com/torvalds/linux/archive/refs/heads/master.zip";

    private TextBox prefixBox;
    private TextBox urlBox;
    private TextBox resultBox;
    private ComboBox presetBox;
    private TextBlock testText;
    private TextBlock tipText;
    private Button testBtn;
    private Button defaultBtn;
    private Button convertBtn;
    private Button restoreBtn;
    private Button copyBtn;
    private Button clearBtn;
    private Button closeBtn;

    private bool busy;

    public MirrorWindow()
    {
        AvaloniaXamlLoader.Load(this);

        prefixBox = this.FindControl<TextBox>("PrefixBox");
        urlBox = this.FindControl<TextBox>("UrlBox");
        resultBox = this.FindControl<TextBox>("ResultBox");
        presetBox = this.FindControl<ComboBox>("PresetBox");
        testText = this.FindControl<TextBlock>("TestText");
        tipText = this.FindControl<TextBlock>("TipText");
        testBtn = this.FindControl<Button>("TestBtn");
        defaultBtn = this.FindControl<Button>("DefaultBtn");
        convertBtn = this.FindControl<Button>("ConvertBtn");
        restoreBtn = this.FindControl<Button>("RestoreBtn");
        copyBtn = this.FindControl<Button>("CopyBtn");
        clearBtn = this.FindControl<Button>("ClearBtn");
        closeBtn = this.FindControl<Button>("CloseBtn");

        List<string> presets = new List<string>();
        for (int i = 0; i < Config.MirrorPresets.Length; i++)
        {
            presets.Add(Config.MirrorPresets[i]);
        }
        presetBox.ItemsSource = presets;

        string current = MirrorStore.Load();
        prefixBox.Text = current;
        tipText.Text = "当前默认：" + current;
        for (int i = 0; i < presets.Count; i++)
        {
            if (string.Equals(presets[i], current, StringComparison.OrdinalIgnoreCase))
            {
                presetBox.SelectedIndex = i;
            }
        }

        presetBox.SelectionChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is string s && s.Length > 0)
            {
                prefixBox.Text = s;
                testText.Text = "";
            }
        };

        testBtn.Click += async (_, _) => await TestMirror();
        defaultBtn.Click += (_, _) => SetDefault();
        convertBtn.Click += (_, _) => Convert();
        restoreBtn.Click += (_, _) => Restore();
        copyBtn.Click += async (_, _) => await Copy();
        clearBtn.Click += (_, _) =>
        {
            urlBox.Text = "";
            resultBox.Text = "";
        };
        closeBtn.Click += (_, _) => Close();
    }

    private string Prefix()
    {
        return MirrorStore.Normalize(prefixBox.Text);
    }

    private async Task TestMirror()
    {
        if (busy)
        {
            return;
        }
        busy = true;
        testBtn.IsEnabled = false;
        testText.Text = "测速中…";
        testText.Foreground = new SolidColorBrush(Color.Parse("#616161"));

        string prefix = Prefix();
        string result = await Task.Run(() => Net.ProbeMirror(prefix, SampleUrl));

        busy = false;
        testBtn.IsEnabled = true;
        if (result.StartsWith("200|"))
        {
            string ms = result.Substring(4);
            testText.Text = "可用 · " + ms + " ms";
            testText.Foreground = new SolidColorBrush(Color.Parse("#0F7B0F"));
        }
        else
        {
            string[] parts = result.Split('|');
            string message = parts.Length > 1 ? parts[1] : "不可用";
            string ms = parts.Length > 2 ? (" " + parts[2] + " ms") : "";
            testText.Text = "不可用：" + message + ms;
            testText.Foreground = new SolidColorBrush(Color.Parse("#C42B1C"));
        }
    }

    private void SetDefault()
    {
        string prefix = Prefix();
        MirrorStore.Save(prefix);
        tipText.Text = "当前默认：" + prefix;
        testText.Text = "已设为默认";
        testText.Foreground = new SolidColorBrush(Color.Parse("#0F7B0F"));
    }

    private void Convert()
    {
        string url = urlBox.Text;
        if (string.IsNullOrWhiteSpace(url))
        {
            resultBox.Text = "请先填写原始链接。";
        }
        else
        {
            resultBox.Text = MirrorStore.Apply(Prefix(), url.Trim());
        }
    }

    private void Restore()
    {
        string url = urlBox.Text;
        if (string.IsNullOrWhiteSpace(url))
        {
            resultBox.Text = "请先填写转换过的链接。";
        }
        else
        {
            resultBox.Text = MirrorStore.Strip(url.Trim());
        }
    }

    private async Task Copy()
    {
        string text = resultBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        try
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null && topLevel.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
            testText.Text = "已复制到剪贴板";
            testText.Foreground = new SolidColorBrush(Color.Parse("#0F7B0F"));
        }
        catch
        {
            testText.Text = "复制失败，请手动选中";
        }
    }
}
