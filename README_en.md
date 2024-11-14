# WINtp — NTP time synchronization client, providing a faster implementation for time synchronization.

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=17&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](README.md) | [English](README_en.md) ]

## It is a simple clock synchronization tool, currently suitable for Windows operating systems

Because the default time synchronization in Windows seems a bit slow,

I created this tool. It now supports retrieving time from websites using the TCP protocol over HTTP(s).

## Downloads

- [Github](https://github.com/lalakii/WINtp/releases)
- [Lanzou 1](https://a01.lanzoui.com/iG6vb2f3mgwb)
- [Lanzou 2](https://a01.lanzout.com/iG6vb2f3mgwb)
- [Lanzou 3](https://a01.lanzouv.com/iG6vb2f3mgwb)

### How to use it?

Typically, you just need to double-click to run it. The software has no UI interface; it will automatically synchronize the system time after starting.

Once completed, it exits immediately and runs no background processes.

It is recommended to install it as a system service, but you can also set it as a startup item according to your preference.

### Configuration

```conf
# A simple time synchronization tool: WINtp.
#
# This configuration file is required to store the configuration parameters needed for the program to run.
# If you need to restore it to default values, simply delete it, and a new one will be automatically generated upon program startup.
# If you want to customize the NTP server, just add it following the pattern:
#
Ntps = time.asia.apple.com;ntp.tencent.com;ntp.aliyun.com;rhel.pool.ntp.org;

# The "Automatic Time Synchronization" switch will not sync the system time if commented out or set to false (to prevent antivirus false positives).
#
AutoSyncTime = true

# The default unit for "Scheduled Synchronization" is seconds, with 3600 seconds meaning synchronization occurs once per hour. It is disabled by default and will only take effect when the service is running.
#
# Delay = 3600

# If, for some reason, you cannot access the UDP port, you can uncomment the line below, and the program will retrieve the time via TCP protocol from the specified website.
# This option does not conflict with NTP; both can coexist or be used as alternatives.
# If NTP can synchronize the time, do not enable this option.
#
# Urls = www.baidu.com;www.qq.com;www.google.com;
#
# The default is to retrieve data from port 80. If you need to use port 443, uncomment the line below.
#
# UseSSL = true

# If you need to print detailed information, uncomment the line below.
#
# Verbose = true

# If you need to customize the timeout, modify the parameter below, in milliseconds
# You can increase the value if the network is poor
# The default timeout is 30 seconds; if the time cannot be obtained, the program will exit and print an error message in the event viewer.
#
# Timeout = 30000

####################
#   by lalaki.cn  ##
####################
```

### By lalaki.cn
