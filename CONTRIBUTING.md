# 参与贡献

感谢你愿意花时间改进 FastGithub。

## 提 issue 之前

1. 先搜一下 [Issues](../../issues) 里有没有相同问题；
2. 带上这些信息（缺了基本没法定位）：
   - Windows 版本（`winver` 看）、是否开了系统代理 / 挂代理软件；
   - 出问题的域名（`github.com` / `raw.githubusercontent.com` / `codeload.github.com` …）；
   - 主界面日志区最后 10 行（截图或复制）；
   - 是否装过 Steam++ / Watt Toolkit 之类也改 hosts 的软件。

## 提 PR

- 大的改动先开 issue 说思路，别直接甩一个几百行的 PR；
- 一个 PR 只做一件事（修 bug / 加功能 / 改文案，别混在一起）；
- 代码风格跟现有文件保持一致：4 空格缩进、`{` 不换行、字段小驼峰、中文注释；
- 新增逻辑要能在**没装 .NET SDK 之外的机器**上编译通过：提交前跑一遍

  ```powershell
  dotnet build -c Release
  ```

- 涉及 hosts 写入、IP 探测顺序、镜像前缀的改动，请在 PR 描述里写清楚对"可还原性"的影响。

## 不要提交

`bin/`、`obj/`、`publish/`、任何 `.exe`、`nodes.txt`、`mirror.txt`、`hosts.fastgithub.bak`。
这些都在 `.gitignore` 里，别用 `git add -f` 强行加进来。

## 许可证

提交代码即表示你同意以本项目的 [MIT](LICENSE) 许可证发布你的贡献。
