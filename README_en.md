# WINtp — NTP time synchronization client, providing a faster implementation for time synchronization.

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=17&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](./README.md) | [English](./README_en.md) ]

## It is a simple clock synchronization tool, currently suitable for Windows operating systems

Because the default time synchronization in Windows seems a bit slow,

I created this tool. It now supports retrieving time from websites using the TCP protocol over HTTP(s).

## Downloads

- [Github](https://github.com/lalakii/WINtp/releases)
- [Lanzou 1](https://a01.lanzoui.com/i7vaf3fwuffg)
- [Lanzou 2](https://a01.lanzout.com/i7vaf3fwuffg)
- [Lanzou 3](https://a01.lanzouv.com/i7vaf3fwuffg)

## How to use it?

The software's main interface allows you to configure various parameters; however, it will not be displayed when the software is used as a service or a startup item.

It is recommended to install it as a system service, but you can also set it as a startup item (by adding the -d parameter), depending on your preference.

## Configuration

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
	<appSettings>
		<add key="AutoSyncTime" value="false" />
		<!-- The "Automatic Time Synchronization" switch will not sync the system time if commented out or set to false (to prevent antivirus false positives).-->
		<add key="Verbose" value="false" /> <!-- Print logs in Event Manager -->
		<add key="UseSsl" value="false" /> <!-- Using the HTTPS protocol -->
		<add key="Delay" value="3600" /> <!-- Time synchronization period, default 3600 seconds. -->
		<add key="Timeout" value="30000" /> <!-- The timeout for a single request is 30,000 milliseconds (30 seconds) by default. -->
		<add key="NetworkTimeout" value="5000" /> <!-- Network timeout, default 5000 milliseconds -->
		<add key="Ntps" value="time.asia.apple.com;ntp.tencent.com;ntp.aliyun.com;rhel.pool.ntp.org;" /> <!-- NTP Servers -->
		<add key="Agreement" value="0" /> <!-- 0 NTP Only, 1 HTTP Only, 2 Both -->
		<add key="Urls" value="www.baidu.com;www.qq.com;www.google.com;" /> <!-- WebWebsite domain(hostname) -->
	</appSettings>
</configuration>
```

### By lalaki.cn
