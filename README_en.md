# WINtp —— NTP time synchronization client, a faster implementation of time synchronization.

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=18&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](README.md) | [English](README_en.md) ]

## It is a simple clock synchronization tool, currently suitable for Windows operating system.

Because the default time synchronization in Windows seems a bit slow, I created it.

## Download

- [Github](https://github.com/lalakii/WINtp/releases)
- [Lanzou 1](https://a01.lanzoui.com/iHXP02ecxgwf)
- [Lanzou 2](https://a01.lanzout.com/iHXP02ecxgwf)
- [Lanzou 3](https://a01.lanzouv.com/iHXP02ecxgwf)

### How to use it?

Typically, you just need to double-click to run it. The software has no UI interface; it will automatically synchronize the system time upon startup.

After completion, it exits immediately and runs no background processes.

It is recommended to install it as a system service, or you can set it to start at boot according to your preference.

### Configuration File Example

```conf
# A Simple NTP Client【WINtp】
#
# This configuration file is not mandatory. If you do not need configuration, you can clear it, just keep the time synchronization setting: AutoSyncTime = true.
#
# By default, the built-in NTP server addresses are "time.asia.apple.com", "time.windows.com", and "rhel.pool.ntp.org".
#
# If you want to customize the NTP servers, you can write them directly in this file, one per line.
#
# You can first use ping to test the latency of the NTP servers.


cn.pool.ntp.org
asia.pool.ntp.org
cn.ntp.org.cn
ntp.aliyun.com
time.apple.com
time.cloudflare.com
centos.pool.ntp.org


# Automatic system time synchronization switch. If uncommented, time synchronization will not occur (this configuration item was added to prevent false positives from antivirus software).
#
AutoSyncTime = true
#
# If you do not want to use my built-in NTP address, uncomment the line below.
#
# useDefaultNtpServer = false
#
# If you need to print detailed information, uncomment the line below.
#
# verbose = true
#
####################
#   by lalaki.cn  ##
####################
```

### By lalaki.cn
