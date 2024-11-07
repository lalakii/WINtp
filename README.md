# WINtp —— NTP 时间同步客户端，更加快速的时间同步实现

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=18&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](README.md) | [English](README_en.md) ]

## 它是一个简单的时钟同步小工具，目前适用于 Windows 操作系统

因为 Windows 默认的时间同步似乎有点缓慢，所以我制作了它，

它现在支持通过 TCP 协议，从 HTTP(s) 协议的网站上获取时间。

## 下载地址

- [本地下载](https://github.com/lalakii/WINtp/releases)
- [蓝奏云 1](https://a01.lanzoui.com/iqehF2egumoj)
- [蓝奏云 2](https://a01.lanzout.com/iqehF2egumoj)
- [蓝奏云 3](https://a01.lanzouv.com/iqehF2egumoj)

### 如何使用它？

通常只需要双击运行即可，软件无 UI 界面，启动后会自动同步系统时间

完成后立即退出，无后台

推荐将其安装为系统服务，也可以自行设置为开机启动项，可根据自己的喜好决定。

### 配置文件示例

```conf
# 一个简单的时间同步小工具 WINtp
#
# 此配置文件是必须的，用于存储程序运行所需的配置参数
# 如果需要将其恢复默认值，直接删除它，程序启动后会自动生成一份新的
# 如果你想要自定义 NTP 服务器，按照规律添加即可:
#
Ntps = time.asia.apple.com;ntp.tencent.com;ntp.aliyun.com;rhel.pool.ntp.org;

# 【自动同步系统时间】开关，如果被注释了，或是值为 false 将不会同步系统时间（防止杀软误报添加这个配置项）
#
AutoSyncTime = true

# 【定时同步】默认单位是秒，3600 秒即每小时同步一次。默认不开启，以服务运行时才会生效
#
# Delay = 3600

# 如果因为某些原因，无法访问 UDP 端口
# 这时你可以取消下面的注释，程序将通过 TCP 协议，从指定的网站上获取时间
# 这个选项不会与 Ntps = 冲突，它们可以同时存在，也可以二选一
# 如果 NTP 可以同步时间，就不要启用它。
#
# Urls = www.baidu.com;www.qq.com;www.google.com;

# 默认从 80 端口获取数据，如需使用 443，取消下面的注释
#
# UseSSL = true

# 如果【需要打印详细信息】，取消下面的注释
#
Verbose = true

# 如果【需要自定义超时】，修改下面的参数，单位是毫秒
# 网络较差时可将数值调大一些
# 默认30秒无法获取时间即超时，程序退出，并在事件查看器打印错误信息
#
# Timeout = 30000

####################
#   by lalaki.cn  ##
####################
```

### By lalaki.cn
