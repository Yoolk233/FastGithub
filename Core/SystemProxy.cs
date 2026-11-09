using System;
using Microsoft.Win32;

namespace FastGithub;

/// <summary>
/// 只读地看一眼系统代理设置。
/// 很多"机场 / 加速器"客户端会把系统代理写成 127.0.0.1:端口，退出时又不还原；
/// 那个代理一死，所有网站都打不开或者被重定向——但用户只会看到"开了这个加速器就坏了"。
/// 我们只读、不写，用来在日志里把这条冲突讲清楚。
/// </summary>
public static class SystemProxy
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    /// <summary>返回系统代理描述，未启用代理时返回空字符串</summary>
    public static string Describe()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                if (key == null)
                {
                    return "";
                }
                object pac = key.GetValue("AutoConfigURL");
                if (pac != null && pac.ToString().Trim().Length > 0)
                {
                    return "自动配置脚本 " + pac.ToString().Trim();
                }
                object enable = key.GetValue("ProxyEnable");
                bool on = enable != null && Convert.ToInt32(enable) != 0;
                object server = key.GetValue("ProxyServer");
                if (on && server != null && server.ToString().Trim().Length > 0)
                {
                    return server.ToString().Trim();
                }
            }
        }
        catch
        {
        }
        return "";
    }
}
