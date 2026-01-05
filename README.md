# WINtp —— NTP 时间同步客户端，更加快速的时间同步实现

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=17&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](./README.md) | [English](./README_en.md) ]

## 适用于 Windows 操作系统的时钟同步小工具

因为 Windows 默认的时间同步似乎有点缓慢，所以我制作了它，

除了支持 NTP 以外，还支持从 HTTP(s) 协议的网站上获取时间。

## 下载地址

- [本地下载](https://github.com/lalakii/WINtp/releases)
- [蓝奏云 1](https://a01.lanzout.com/iM5Qw3fc4owb)
- [蓝奏云 2](https://a01.lanzoui.com/iM5Qw3fc4owb)
- [蓝奏云 3](https://a01.lanzouv.com/iM5Qw3fc4owb)

## 如何使用？

通常只需要双击运行即可，软件主界面可以配置各项参数

推荐将其安装为系统服务，也可以自行设置为开机启动项(加 -d 参数)，可根据自己的喜好决定。

## 配置文件示例

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
	<appSettings>
		<add key="AutoSyncTime" value="false" />
		<!-- 自动同步系统时间的开关，此参数默认为 false 目的为了防止杀软误报，用户应当手动设为true -->
		<add key="Verbose" value="false" /> <!-- 在事件管理器中打印日志 -->
		<add key="UseSsl" value="false" /> <!-- 使用https协议 -->
		<add key="Delay" value="3600" /> <!-- 时间同步周期，默认3600秒 -->
		<add key="Timeout" value="30000" /> <!-- 单个请求超时，默认30000毫秒(30秒) -->
		<add key="NetworkTimeout" value="5000" /> <!-- 网络超时，默认5000毫秒(5秒) -->
		<add key="Ntps" value="time.asia.apple.com;ntp.tencent.com;ntp.aliyun.com;rhel.pool.ntp.org;" /> <!-- ntp服务器 -->
		<add key="Urls" value="www.baidu.com;www.qq.com;www.google.com;" /> <!-- 网站域名 -->
	</appSettings>
</configuration>
```

## By lalaki.cn
