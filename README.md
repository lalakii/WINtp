# WINtp —— NTP 时间同步客户端，更加快速的时间同步实现

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=18&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](README.md) | [English](README_en.md) ]

## 它是一个简单的时钟同步小工具，目前适用于 Windows 操作系统

因为 Windows 默认的时间同步似乎有点缓慢，所以我制作了它。

## 下载地址

- [本地下载](https://github.com/lalakii/WINtp/releases)
- [蓝奏云 1](https://a01.lanzoui.com/iHXP02ecxgwf)
- [蓝奏云 2](https://a01.lanzout.com/iHXP02ecxgwf)
- [蓝奏云 3](https://a01.lanzouv.com/iHXP02ecxgwf)

### 如何使用它？

通常只需要双击运行即可，软件无 UI 界面，启动后会自动同步系统时间

完成后立即退出，无后台

推荐将其安装为系统服务，也可以自行设置为开机启动项，可根据自己的喜好决定。

### 配置文件示例

```conf
# 一个简单的时间同步小工具【WINtp】
#
# 此配置文件不是必须的，如果你不需要配置，可以将其清空，只需要保留时间同步 AutoSyncTime = true
#
# 默认内置了"time.asia.apple.com", "time.windows.com", "rhel.pool.ntp.org"，这3个ntp服务器地址
#
# 如果你想要自定义ntp服务器，可以直接写在这个文件里面，每行一个
#
# 可以先用ping测试ntp服务器的延迟怎么样噢


cn.pool.ntp.org
asia.pool.ntp.org
cn.ntp.org.cn
ntp.aliyun.com
time.apple.com
time.cloudflare.com
centos.pool.ntp.org


# 自动同步系统时间开关，如果取消注释将不会同步时间（防止杀软误报添加了这个配置项）
#
AutoSyncTime = true
#
# 如果【不想使用我内置的ntp地址】，取消注释掉下面的行
#
# useDefaultNtpServer = false
#
# 如果【需要打印详细信息】，取消注释下面的行
#
# verbose = true
#
####################
#   by lalaki.cn  ##
####################
```

### By lalaki.cn
