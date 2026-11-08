# FastGithub

一个 Windows 桌面小工具，解决国内访问 GitHub 时 **网页打不开/加载慢** 和 **Release、raw、archive 下载龟速** 两个问题。

- 网页访问：测速 GitHub 相关域名，挑出当前可用的 IP，写进系统 `hosts`，浏览器直连 `github.com`
- 下载加速：把 `Release` / `raw` / `archive` 链接套上镜像前缀，走公益镜像下载
- 一键还原：hosts 里所有改动都包在带标记的块内，点一下「停止并还原」即回到原样

界面基于 [Avalonia](https://avaloniaui.net/) 11.2.3，单文件发布，双击即用；无需安装运行时（发布包已自包含）。

## 功能

| 功能 | 说明 |
| --- | --- |
| 一键加速 | 并发测速 21 个 GitHub 域名，写 hosts，刷新 DNS 缓存 |
| 节点状态表 | 实时显示每个域名的优选 IP、TCP 延迟、复验结果 |
| 重新测速 | 只测速不写 hosts，先看看当前网络哪条 IP 能通 |
| 下载加速 | 内置 6 个镜像前缀，可切换、可自定义、可测速 |
| 停止还原 | 删除加速块，并把被临时注释的第三方条目原样放开 |
| 自动自愈 | 每 60 秒检查一次 hosts，被别的软件改写后按缓存自动补回 |

## 工作原理

**1. 网页直连（hosts）**

对每个域名，候选 IP = 内置 IP 池 ∪ 阿里/腾讯 DoH 解析结果，然后：

1. TCP 443 连接初筛（1500ms 超时），只留下能连通的；
2. 对最快的 6 个做 TLS 1.2 握手 + `GET /` 复验：
   - `200 ~ 399` → `GOOD`（可用）
   - `403 / 404` → `WEAK`（能连但受限，仍算可用）
   - 其他 → `BAD`
3. 取最优的一个写入 hosts：

```
# ==== FastGithub BEGIN (auto generated) ====
<ip>\tgithub.com
...
# ==== FastGithub END ====
```

**2. 下载加速（镜像前缀）**

原来的镜像站只做一件事：把 `https://github.com/user/repo/...` 变成
`https://ghfast.top/https://github.com/user/repo/...`。所以本工具不代理流量，只做链接改写，点「下载加速」打开转换窗口即可。

## 构建

需要 **.NET 8 SDK** 和 Windows（hosts 路径、`DnsFlushResolverCache` 都是 Windows 专有）。

```powershell
# 还原依赖并编译
dotnet build -c Release

# 打一个自包含单文件（和发布包一致）
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

产物在 `bin\Release\net8.0\win-x64\`，双击 `FastGithub.exe` 会弹 UAC（写 hosts 需要管理员权限）。

也可以直接跑源码调试：

```powershell
dotnet run
```

## 命令行

给脚本/批处理用，免开界面：

```powershell
# 测速并写入 hosts
.\FastGithub.exe --apply

# 还原 hosts
.\FastGithub.exe --restore

# 只测速，把结果写到文件
.\FastGithub.exe --selftest --out=D:\selftest.txt
```

`--apply` / `--restore` 的日志写在 `%TEMP%\fastgithub_cli.txt`。

## 数据文件

全部放在 `%APPDATA%\FastGithub\`：

| 文件 | 内容 |
| --- | --- |
| `nodes.txt` | 上次写入 hosts 的 域名 → IP，用于 60 秒自愈 |
| `mirror.txt` | 上次设为默认的镜像前缀 |

hosts 原文件第一次被改写前会备份为 `C:\Windows\System32\drivers\etc\hosts.fastgithub.bak`（仅备份一次，之后不覆盖，避免把已被改写的版本当成"原始版"）。

## 注意事项

- **需要管理员权限**：程序清单里声明了 `requireAdministrator`，所有操作都在你本机完成，不联网上传任何数据。
- **会修改系统 hosts**：所有改动都在 `# ==== FastGithub BEGIN ====` 与 `# ==== FastGithub END ====` 之间，随时可一键还原；第三方软件（如 Steam++、 Watt Toolkit）已经写过的同名域名会被临时注释成 `#FG# ...`，还原时原样放开。
- **可能被杀软误报**：单文件自解压 + 申请管理员权限 + 改 hosts，是典型的"会被误杀"组合。遇到拦截请自行加白名单，或从源码自行编译。
- **镜像站是第三方公益服务**，可用性不由本项目保证；界面里的「测速」就是用来判断当下哪个能用的。
- 本项目不提供任何翻墙/代理能力，也不修改 DNS 服务器，只做 IP 择优与链接改写。

## 目录结构

```
FastGithub/
├─ Program.cs            入口、CLI 分支、Avalonia 初始化
├─ App.axaml(.cs)        应用与主题
├─ MainWindow.axaml(.cs) 主窗口（状态卡 / 节点表 / 日志）
├─ MirrorWindow.axaml(.cs) 下载加速窗口（镜像切换 + 链接转换）
├─ Core/
│  ├─ Config.cs          域名表、IP 池、镜像预设、hosts 标记
│  ├─ Node.cs            节点模型
│  ├─ Net.cs             DoH、TCP 初筛、TLS 复验、镜像测速
│  ├─ Engine.cs          择优、写 hosts、缓存、自愈
│  ├─ Hosts.cs           hosts 读写/备份/冲突处理
│  ├─ MirrorStore.cs     镜像前缀持久化与链接改写
│  └─ Dns.cs             DnsFlushResolverCache
├─ Themes/Fluent.xaml    主按钮等自定义样式
└─ app.manifest          requireAdministrator + PerMonitorV2 DPI
```

## 许可

[MIT](LICENSE)。思路参考了 [dotnetcore/FastGithub](https://github.com/dotnetcore/FastGithub)（MIT）等同类开源项目，代码为独立实现。
