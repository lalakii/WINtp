# WINtp — NTP time synchronization client, providing a faster implementation for time synchronization.

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=18&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](tree/README.md) | [English](tree/README_en.md) ]

## It is a simple clock synchronization tool, currently suitable for Windows operating systems

Because the default time synchronization in Windows seems a bit slow, I created this tool. It now supports retrieving time from websites using the TCP protocol over HTTP(s).

## Downloads

- [Github](https://github.com/lalakii/WINtp/releases)
- [Lanzou 1](https://a01.lanzoui.com/iqtKI2ef4bkh)
- [Lanzou 2](https://a01.lanzout.com/iqtKI2ef4bkh)
- [Lanzou 3](https://a01.lanzouv.com/iqtKI2ef4bkh)

### How to use it?

Typically, you just need to double-click to run it. The software has no UI interface; it will automatically synchronize the system time after starting.

Once completed, it exits immediately and runs no background processes.

It is recommended to install it as a system service, but you can also set it as a startup item according to your preference.

### Configuration

```conf
# A simple time synchronization tool WINtp
#
# This configuration file is required to store the configuration parameters needed for the program to run
#
# If you need to restore the default values, simply delete this file, and a new one will be automatically generated when the program starts
#
# If you want to customize the NTP server, just add it according to the format.

Ntps = time.asia.apple.com;ntp.tencent.com;ntp.aliyun.com;rhel.pool.ntp.org;

# The [Automatic System Time Synchronization] switch. If it is commented out or set to false, the system time will not be synchronized (this configuration item is added to prevent false positives from antivirus software).

AutoSyncTime = true

# If, for some reason, you are unable to access the UDP port.
# At this point, you can uncomment the line below, and the program will retrieve the time from the specified website using the TCP protocol.
# This option will not conflict with "Ntps =", they can coexist or be used as alternatives.
# When the UDP port is accessible, it is generally not recommended to enable this option.
#
# Urls = www.baidu.com;www.qq.com;www.google.com;
#
# By default, data is retrieved from port 80. To use port 443, uncomment the line below.
#
# UseSSL = true
#
# If [Detailed Information Printing] is needed, uncomment the line below.
#
# Verbose = true
#
# If [Custom Timeout] is needed, modify the parameter below. The unit is milliseconds. By default, if the time cannot be retrieved within 30 seconds, the program will time out and exit.
# You can increase the value when the network is poor.
#
# Timeout = 30000
#
####################
#   by lalaki.cn  ##
####################
```

### By lalaki.cn
