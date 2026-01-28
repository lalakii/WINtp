# WINtp —— NTP 时间同步客户端，更加快速的时间同步实现

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

[<img alt="Download WINtp" src="https://sourceforge.net/sflogo.php?type=17&amp;group_id=3814875" width=268></a>](https://sourceforge.net/projects/wintp/)

[<img alt="WINtp Logo" src="https://fastly.jsdelivr.net/gh/lalakii/WINtp@master/wintp.jpg" width=70></a>](https://sourceforge.net/projects/wintp/)

[ [中文](./README.md) | [English](./README_en.md) ]

## 适用于 Windows 操作系统的时间同步小工具

因为 Windows 默认的时间同步似乎有点缓慢，所以我制作了它，

除了支持 NTP 以外，还支持从 HTTP(s) 协议的网站上获取时间。

## 下载地址

- [本地下载](https://github.com/lalakii/WINtp/releases)
- [蓝奏云 1](https://a01.lanzoui.com/iSrAb3h7qqxi)
- [蓝奏云 2](https://a01.lanzout.com/iSrAb3h7qqxi)
- [蓝奏云 3](https://a01.lanzouv.com/iSrAb3h7qqxi)

## 如何使用？

软件主界面可以配置各项参数，作为服务或开机启动项时不会显示主界面

推荐将其安装为系统服务，也可以自行设置为开机启动项(加 -d 参数)，可根据自己的喜好决定。

## 时间同步设置

配置文件已改为JSON格式，建议在图形界面上配置参数。

### 同步方式

- **停止时间同步**  
  完全关闭时间同步功能，软件不再对系统时间做任何调整。

- **立即修改时间**  
  获取到准确的网络时间后，立即强制将系统时间设置为网络时间（step / 跳变方式）。  
  *注意*：时间跳变可能导致某些对时间敏感的应用程序（数据库、日志系统、日历软件等）出现异常。

- **渐进加速优先（高精度）**  
  仅适用于Win11操作系统。  
  通过逐渐加速或减慢系统时钟的速率（slew / 渐进调整），让本地时间平滑地趋近网络时间。  
  能有效避免时间跳变，适合对时钟连续性要求较高的场景。

- **渐进加速优先**  
  与高精度模式行为相同，但采用旧版本的实现算法，用于兼容早期版本的用户或特定旧系统环境。

### 时间偏移量
允许用户手动设置一个固定的时间偏移（单位通常为秒或毫秒）。

- 正值：本地时间比网络时间**提前**（快多少秒/毫秒）
- 负值：本地时间比网络时间**滞后**（慢多少秒/毫秒）

### 误差范围
当网络时间与本地时间的差值**小于**此范围时，软件将**不会**执行同步操作。

- 推荐值：45 ms
- 作用：避免频繁设置系统时间

### 优先级
数字越小优先级**越高**，优先从优先级高的地址获取时间

### 禁用 Windows Time 服务
勾选后禁用Windows自带的时间同步服务
...

## By lalaki.cn
