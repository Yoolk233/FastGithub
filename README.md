# FastGithub 1.2.2Criticalissuefix

> 界面截图请自行运行程序后截取（仓库内不附带截图，避免与真实版本不一致）。

一个 Windows 桌面小工具，解决国内访问 GitHub 时 **网页打不开/加载慢** 和 **Release、raw、archive 下载龟速** 两个问题。

- 网页访问：测速 GitHub 相关域名，挑出当前**真的能返回网页**的 IP，写进系统 `hosts`，浏览器直连 `github.com`
- 下载加速：把 `Release` / `raw` / `archive` 链接套上镜像前缀，走公益镜像下载
- 一键还原：hosts 里所有改动都包在带标记的块内，点一下「停止还原」即回到原样

界面基于 [Avalonia](https://avaloniaui.net/) 11.2.3，单文件发布，双击即用；无需安装运行时（发布包已自包含）。

## 1.2.2 修了什么

这一版的出发点是**把不属于本工具、但会被算到本工具头上的问题分开**，同时清掉确实不干净的东西：

| 问题 | 之前的做法 | 1.2.2 的做法 |
| --- | --- | --- |
| 内置镜像域名不干净 | 内置 6 个第三方 ghproxy 域名 | **全部移除**：实测 5 个超时/502、`ghp.ci` 域名失效。这类死链点开就是广告页甚至博彩跳转页，不再随程序分发；需要镜像由用户自己填可信前缀，留空＝不使用镜像 |
| 状态码 200 但内容不对 | 只判断 `200~399` | `github.com` 追加**内容指纹校验**（页面里必须出现 GitHub 特征），否则判 BAD 不写 hosts |
| 加速完才发现打不开 | 写入后不立即复验 | 写完 hosts + 清 DNS 缓存后**立刻复验 5 个核心域名**，失败就列出域名并提示"疑似上游 DNS 被劫持"，界面同步标为「加速已失效」 |
| 分不清是谁搞坏了网络 | 只把别人的 hosts 条目注释掉，不说明 | 启动时扫描 hosts 里指向 `127.0.0.1` / `::1` 的条目、只读检查系统代理，并在日志里点名"这是别的加速器留下的" |
| 用户不知道作用范围 | 无提示 | 启动日志固定输出：只接管 21 个 GitHub 域名，**不改系统 DNS、不设系统代理、不碰其它网站的解析** |

## 1.2.1Fix2 修了什么

1.2.0 最常被反馈的问题是「点了加速，GitHub 反而打不开」：浏览器显示 404、连接超时，或者用着用着突然断连，但界面上还写着"已加速"。原因和修法：

| 问题 | 1.2.0 的做法 | 1.2.1Fix2 的做法 |
| --- | --- | --- |
| 网页打不开、整站 404 | `403 / 404` 只算"受限（WEAK）"，仍然写进 hosts。IP 能连上但不服务 `github.com` 的虚拟主机，浏览器拿到的就是 404 | **网页型域名（`github.com` / `api.github.com` / `gist.github.com` / `github.io`）必须复验出 `200~399` 才允许写入**，其余一律不写 |
| 复验路径不真实 | 所有域名都用 `GET /` | `github.com` 改用真实页面 `GET /login` 复验，避免边缘节点对根路径返回 404 造成误判 |
| 突然断连、界面还显示已加速 | 60 秒只检查"hosts 加速块还在不在"，从不验证节点是否还有效 | 每 **45 秒**对 5 个核心域名做一次真实 TLS 复验；节点失效就**自动重新择优换 IP 并重写 hosts**，界面状态同步成"加速已失效 / 自动换节点中"，换好后再回到"已加速" |
| TLS 只支持 1.2 | 仅 `Tls12` | `Tls12 \| Tls13`，握手更贴近浏览器行为 |
| 测速结果口径 | 只有"可用/受限/不可用" | 表格改用 `Acceptable` 口径，测速完成后直接告诉你"可写入 N 个域名" |

## 功能

| 功能 | 说明 |
| --- | --- |
| 侧边栏 | 「主界面」与「设置」两页，主界面内容与原版一致 |
| 设置 · 显示 | 6 种强调色可选，点一下立即生效并记住（存在 `%APPDATA%\FastGithub\ui.txt`）；有返回按钮 |
| 一键加速 | 并发测速 21 个 GitHub 域名，写 hosts，刷新 DNS 缓存 |
| 节点状态表 | 实时显示每个域名的优选 IP、TCP 延迟、复验结果 |
| 重新测速 | 只测速不写 hosts，先看看当前网络哪条 IP 能通 |
| 下载加速 | 不内置任何第三方镜像域名；填自己的可信前缀即可，可测速、可一键还原 |
| 停止还原 | 删除加速块，并把被临时注释的第三方条目原样放开 |
| 自动自愈 | 每 60 秒检查 hosts 是否被别的软件改写，按缓存补回 |
| 节点巡检 | 每 45 秒复验已写入的节点，失效自动换 IP（见上表） |

## 工作原理

**1. 网页直连（hosts）**

对每个域名，候选 IP = 内置 IP 池 ∪ 阿里/腾讯 DoH 解析结果，然后：

1. TCP 443 连接初筛（1500ms 超时），只留下能连通的；
2. 对最快的 6 个做 TLS 握手 + `GET <路径>` 复验（`github.com` 用 `/login`，其余用 `/`）：
   - `200 ~ 399` → `GOOD`（可用）
   - `403 / 404` → `WEAK`（能连但受限）
   - 其他 → `BAD`
3. 写入规则：
   - 网页型域名（`Config.IsStrict`）：**只接受 `GOOD`**，`WEAK` / `BAD` / `FAIL` 一律不写；
   - 资源类域名（`raw` / `objects` / `avatars` 等，根路径本来就返回 404/400 是正常的）：`GOOD` 或 `WEAK` 都可以写；
4. 取最优的一个写入 hosts：

```
# ==== FastGithub BEGIN (auto generated) ====
<ip>\tgithub.com
...
# ==== FastGithub END ====
```

5. 每 45 秒巡检 `CoreDomains` 这 5 个域名：`github.com`、`api.github.com`、`codeload.github.com`、`raw.githubusercontent.com`、`github.githubassets.com`。有失效的就重新择优、重写 hosts；一个都换不到时界面改成"加速已失效"，不会继续假装已加速。

**2. 下载加速（镜像前缀）**

原来的镜像站只做一件事：把 `https://github.com/user/repo/...` 变成
`https://<你填的镜像前缀>/https://github.com/user/repo/...`。所以本工具不代理流量，只做链接改写，点「下载加速」打开转换窗口即可。

> 1.2.2 起**不再内置任何第三方镜像域名**：实测原来那 6 个不是超时就是 502，`ghp.ci` 连域名都失效了，点开基本就是广告页。需要镜像请自己填一个可信前缀，程序会先帮你测速再允许使用；不填就原样输出 GitHub 链接。

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

# 按当前 hosts 块巡检核心域名（加 --repair 则自动换节点）
.\FastGithub.exe --health
.\FastGithub.exe --health --repair

# 只测速，把结果写到文件
.\FastGithub.exe --selftest --out=D:\selftest.txt
```

`--apply` / `--restore` 的日志写在 `%TEMP%\fastgithub_cli.txt`，`--health` 的写在 `%TEMP%\fastgithub_health.txt`。

## 数据文件

全部放在 `%APPDATA%\FastGithub\`：

| 文件 | 内容 |
| --- | --- |
| `nodes.txt` | 上次写入 hosts 的 域名 → IP，用于自愈与巡检换节点 |
| `mirror.txt` | 上次设为默认的镜像前缀 |
| `ui.txt` | 设置页「显示」里选的强调色 |

hosts 原文件第一次被改写前会备份为 `C:\Windows\System32\drivers\etc\hosts.fastgithub.bak`（仅备份一次，之后不覆盖，避免把已被改写的版本当成"原始版"）。

## 注意事项

- **需要管理员权限**：程序清单里声明了 `requireAdministrator`，所有操作都在你本机完成，不联网上传任何数据。
- **会修改系统 hosts**：所有改动都在 `# ==== FastGithub BEGIN ====` 与 `# ==== FastGithub END ====` 之间，随时可一键还原；第三方软件（如 Steam++、Watt Toolkit）已经写过的同名域名会被临时注释成 `#FG# ...`，还原时原样放开。
- **可能被杀软误报**：单文件自解压 + 申请管理员权限 + 改 hosts，是典型的"会被误杀"组合。遇到拦截请自行加白名单，或从源码自行编译。
- **镜像站是第三方服务**，本项目不再内置任何镜像域名（原来那些已全部失效），需要就自己填、自己测速。
- **`github.com` 没有可写节点时就是"没有可用节点"**：1.2.2 宁可这次不写 hosts，也不会再写一个会让浏览器 404 的 IP，更不会写一个"状态码 200 但内容不是 GitHub"的 IP。
- **其它网站出现异常不一定是本工具的问题**：本工具只写 21 个 GitHub 域名，不改系统 DNS、不设系统代理。若别的网站跳到广告页 / 博彩页，先点「停止并还原」对比，再检查路由器 DNS、机场客户端与系统代理。
- 本项目不提供任何翻墙/代理能力，也不修改 DNS 服务器，只做 IP 择优与链接改写。

## 目录结构

```
FastGithub/
├─ Program.cs            入口、CLI 分支（--apply / --restore / --health / --selftest）
├─ App.axaml(.cs)        应用、主题与强调色资源
├─ MainWindow.axaml(.cs) 主窗口：左侧栏（主界面 / 设置）+ 状态卡 / 节点表 / 日志
├─ MirrorWindow.axaml(.cs) 下载加速窗口（镜像切换 + 链接转换）
├─ Core/
│  ├─ Config.cs          版本号、域名表、IP 池、镜像预设、网页型域名判定
│  ├─ Node.cs            节点模型（Usable / Healthy / Acceptable）
│  ├─ Net.cs             DoH、TCP 初筛、TLS 复验（支持指定路径）、镜像测速
│  ├─ Engine.cs          择优、写 hosts、缓存、自愈、巡检换节点
│  ├─ Hosts.cs           hosts 读写/备份/冲突处理
│  ├─ MirrorStore.cs     镜像前缀持久化与链接改写
│  └─ Dns.cs             DnsFlushResolverCache
├─ Themes/Fluent.xaml    主按钮 / 侧边栏 / 调色板样式
└─ app.manifest          requireAdministrator + PerMonitorV2 DPI
```

## 许可

[MIT](LICENSE)。思路参考了 [dotnetcore/FastGithub](https://github.com/dotnetcore/FastGithub)（MIT）等同类开源项目，代码为独立实现。
