# WINtp —— NTP 时间同步客户端

[![Latest Version](https://img.shields.io/github/v/release/lalakii/WINtp?logo=github)](https://github.com/lalakii/WINtp/releases)
[![License: Apache-2.0 (shields.io)](https://img.shields.io/badge/License-Apache--2.0-c02041?logo=apache)](LICENSE)

## 一个简单的时钟同步小工具，适用于 Windows 操作系统

## 下载地址

- [本地下载](https://github.com/lalakii/WINtp/releases)
- [蓝奏云 1](https://a01.lanzout.com/ia9cg2e95m6j)
- [蓝奏云 2](https://a01.lanzoui.com/ia9cg2e95m6j)
- [蓝奏云 3](https://a01.lanzouv.com/ia9cg2e95m6j)

### 如何使用它？

通常只需要双击即可，软件无 UI 界面，双击启动后会自动同步系统时间。

### 配置文件示例

```conf
# 配置文件名称 ntp.ini，与应用程序放在同一路径
#
# 此配置文件不是必须的，如果你不需要，可以不用配置。
#
# 默认内置了"time.asia.apple.com", "time.windows.com", "rhel.pool.ntp.org"，这3个ntp服务器地址
#
# 如果你想要自定义ntp服务器，可以直接写在这个文件里面，每行一个

cn.pool.ntp.org
asia.pool.ntp.org
cn.ntp.org.cn
ntp.aliyun.com
time.apple.com
time.cloudflare.com
centos.pool.ntp.org

# 如果【不想使用我内置的ntp地址】，取消注释掉下面的行
#
# useDefaultNtpServer = false
#
# 如果【需要打印详细信息】，取消注释下面的行
#
# verbose = true
#
# 如有需要自行将其注册为系统服务，在开机时启动一次就好~~ 同步完成会立即退出，无后台。
#
#############
#   by lalaki.cn  ##
#############

```

### By lalaki.cn
