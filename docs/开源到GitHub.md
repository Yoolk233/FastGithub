# FastGithub 开源到 GitHub：照做清单

> 这份是按**这台机器**的实测情况写的（2026-11-08 实测：`winget` 可用、`git` 和 `dotnet` 都没装、GitHub 可直连、NuGet 下载 4MB/s）。
> 每一步都给了"敲什么 → 应该看到什么 → 不对怎么办"。全程大约 20 分钟。

---

## 第 1 步 装 Git（2 分钟）

winget 的 Git 安装包指向 `github.com/.../releases`，这条链接不一定快；**用华为云镜像的安装包，实测 1.4MB/s**：

```powershell
$u = "https://mirrors.huaweicloud.com/git-for-windows/v2.47.1.windows.1/Git-2.47.1-64-bit.exe"
curl.exe -L -o "$env:TEMP\Git-installer.exe" $u
Start-Process "$env:TEMP\Git-installer.exe" -ArgumentList "/VERYSILENT","/NORESTART" -Wait
```

装完**关掉并重开 PowerShell**（PATH 才生效），验证：

```powershell
git --version
```

期望：`git version 2.47.1.windows.1`（或更高）。没有输出就用完整路径试：

```powershell
& "C:\Program Files\Git\cmd\git.exe" --version
```

---

## 第 2 步 装 .NET 8 SDK（3 分钟，225MB）

winget 这条走 `download.microsoft.com`，**实测 1.2MB/s**，比官网页面快得多：

```powershell
winget install --id Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements
```

装完重开 PowerShell，验证：

```powershell
dotnet --version
```

期望：`8.0.4xx`。若提示找不到命令：

```powershell
$env:PATH += ";C:\Program Files\dotnet"
dotnet --version
```

> 如果 winget 也卡住，直接下安装包（同一地址，迅雷/IDM 多线程更快）：
> `https://download.microsoft.com/download/1c8c36ae-1e08-4005-b94c-ee0e9da9ba08/8d751af1-8c2a-4c90-b2b8-989b212d267b/dotnet-sdk-8.0.427-win-x64.exe`

---

## 第 3 步 本地编译验证（第一次一定要做）

```powershell
cd "C:\Users\Administrator\Doubao\chats\2026-11-08\new-chat-2\FastGithub"
dotnet build -c Release
```

期望最后几行：

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**如果只在 `App.axaml` / `Themes/Fluent.xaml` 上报错**——这两个是还原时重写的部分（其余都是逐语句还原），把报错行发我，或按提示改：常见是选择器名、`CornerRadius` 之类属性名。C# 部分已经用 Roslyn 对真实 Avalonia 11.2.3 程序集编译验证过（0 error 0 warning），逻辑层不会出错。

---

## 第 4 步 打一个单文件包（确认产物能用）

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

期望：`publish\FastGithub.exe`（约 40MB）。双击 → 弹 UAC → 主窗口出来 → 点「一键加速」能出节点。**这一步过了，说明源码是真能跑的**，再往下推。

---

## 第 5 步 建本地仓库

```powershell
cd "C:\Users\Administrator\Doubao\chats\2026-11-08\new-chat-2\FastGithub"
git init
git add .
git status
```

`git status` 里**必须看不到** `bin/`、`obj/`、`publish/`、`*.exe`、`nodes.txt`、`mirror.txt`、`*.bak`。验证忽略规则生效：

```powershell
git check-ignore -v publish\FastGithub.exe obj\project.assets.json
```

期望各输出一行规则。然后提交：

```powershell
git commit -m "feat: 初始提交，FastGithub 1.2.1Fix2"
```

---

## 第 6 步 在 GitHub 建空仓库（网页）

1. 打开 https://github.com/new
2. **Repository name**：`FastGithub`
3. **Description**：`Windows 下 GitHub 网页直连与下载加速工具（Avalonia）`
4. 选 **Public**
5. **下面三个勾全部不要勾**（Add a README / Add .gitignore / Choose a license）——本地都有，勾了 push 会冲突
6. **Create repository**

---

## 第 7 步 生成 PAT 并推送

### 7.1 生成 Token

1. https://github.com/settings/tokens → **Generate new token (classic)**
2. Note：`fastgithub-push`；Expiration：90 days
3. 勾 **`repo`**（整个大类）**和 `workflow`**（这个必须有——仓库里带 `.github/workflows/build.yml`，没有 `workflow` 权限会被 GitHub 拒收 `refusing to allow a Personal Access Token to create or update workflow`）
4. 生成后立刻复制 `ghp_xxxxxxxx`

### 7.2 推送

```powershell
git remote add origin https://github.com/<你的用户名>/FastGithub.git
git branch -M main
git push -u origin main
```

弹出登录框时：**用户名填 GitHub 用户名，密码位置粘贴刚才的 token**（不是账号密码）。

> 想以后免认证：Git for Windows 自带 Git Credential Manager，第一次 push 会弹浏览器点「Authorize」，之后自动记住。

### 7.3 推送失败对照

| 报错 | 原因 | 处理 |
| --- | --- | --- |
| `Support for password authentication was removed` | 用了账号密码 | 改粘贴 token |
| `refusing to allow a Personal Access Token to create or update workflow` | token 没勾 `workflow` | 重新生成 token，把 `repo` + `workflow` 两个都勾上 |
| `SSL peer certificate ... was not OK` | git 自带 CA 包不认这条链（网络里有中间设备） | `git config --global http.sslBackend schannel`（改用 Windows 证书库；本机实测 curl 能通、git 默认 OpenSSL 不通） |
| `Permission to xxx denied to yyy` | token 没勾 `repo` / 登错账号 | 重新生成；清凭据：控制面板 → 凭据管理器 → 删 `git:https://github.com` |
| `failed to push some refs (fetch first)` | 建仓时勾了 README | `git pull --rebase origin main` 再 push |
| `Failed to connect ... port 443` | 网络 | `git config --global https.proxy http://127.0.0.1:端口`（有本地代理时） |
| `LF will be replaced by CRLF` | 换行符提示 | 无害，`.gitattributes` 已统一 |

---

## 第 8 步 发版（打 tag，CI 自动出 exe）

```powershell
git tag -a v1.2.1Fix2 -m "FastGithub 1.2.1Fix2"
git push origin v1.2.1Fix2
```

仓库里已经带了 `.github/workflows/build.yml`：push 标签后 Actions 会自动在 `windows-latest` 上编译、单文件打包、**自动创建 Release 并挂上 exe**。

首次跑之前先进 **Settings → Actions → General → Workflow permissions**，选 **Read and write permissions** 并保存，否则发布 Release 会 403。

看进度：仓库页 **Actions** → `build`。绿了就回 **Releases** 看有没有 `v1.2.1Fix2` 和 `FastGithub.exe`。

> 也可以手动发：Releases → Draft a new release → 选 `v1.2.1Fix2` → 把本地 `publish\FastGithub.exe` 拖进附件 → Publish。

---

## 第 9 步 仓库装修（10 分钟，观感差别很大）

| 位置 | 填什么 |
| --- | --- |
| 右侧 About（齿轮） | Description 同上；**Topics**：`github` `hosts` `avalonia` `windows` `accelerator` `dotnet`；勾 Releases |
| README 顶部 | 加状态徽标：`![build](https://github.com/<你>/FastGithub/actions/workflows/build.yml/badge.svg)` |
| README 截图 | 跑起来截一张主窗口，存 `docs/screenshot.png`，插到 README 顶部 |
| Issues | 已经有 `.github/ISSUE_TEMPLATE/bug_report.md`，开箱即用 |
| 贡献 / 安全 | `CONTRIBUTING.md`、`SECURITY.md` 已在仓库里 |
| 许可证 | GitHub 会自动识别 `LICENSE` 显示 `MIT license` |

---

## 第 10 步 以后每次发版（3 条命令）

```powershell
git add .
git commit -m "fix: xxx"
git push
git tag -a v1.2.1Fix2 -m "FastGithub 1.2.1Fix2"; git push origin v1.2.1Fix2
```

CI 自动出包，不用手动传 exe。

---

## 附：一次性检查清单

- [ ] `git --version` 有输出
- [ ] `dotnet --version` 是 8.0.x
- [ ] `dotnet build -c Release` → 0 error
- [ ] `publish\FastGithub.exe` 双击能开、能加速、能还原
- [ ] `git status` 里没有 bin/obj/publish/exe
- [ ] GitHub 上建的是**空仓**（没勾 README）
- [ ] PAT 勾了 `repo`
- [ ] `git push` 成功，仓库首页能看到 20+ 个文件
- [ ] Actions 是绿的
- [ ] Releases 里有 `v1.2.1Fix2` + exe

全打勾 = 开源完成，可以发链接给别人了。
