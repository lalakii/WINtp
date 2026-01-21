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
- [Lanzou 1](https://a01.lanzoui.com/iJk6U3goeb0j)
- [Lanzou 2](https://a01.lanzout.com/iJk6U3goeb0j)
- [Lanzou 3](https://a01.lanzouv.com/iJk6U3goeb0j)

## How to use it?

The software's main interface allows you to configure various parameters; however, it will not be displayed when the software is used as a service or a startup item.

It is recommended to install it as a system service, but you can also set it as a startup item (by adding the -d parameter), depending on your preference.

## Time Synchronization Settings

**The configuration file has been changed to JSON format.**  
It is recommended to configure all parameters through the graphical user interface (GUI).

### Synchronization Mode

Controls how the software synchronizes the local system time with the obtained network time.

- **Stop time synchronization**  
  Completely disables the time synchronization feature. The software will make no further adjustments to the system clock.

- **Immediately set time**  
  Once accurate network time is retrieved, the system clock is forcibly updated to match it (step / time jump method).  
  *Note:* Sudden time jumps may cause abnormal behavior in time-sensitive applications (databases, logging systems, calendar software, etc.).

- **Gradual acceleration priority (high precision)**  
  *Applicable only to Windows 11.*  
  Gradually speeds up or slows down the system clock rate (slew / smooth adjustment) so that local time converges smoothly to network time.  
  This method effectively avoids abrupt time jumps and is ideal for scenarios that require strict clock monotonicity and continuity.

- **Gradual acceleration priority**  
  Behaves the same as the high-precision mode, but uses the older implementation/algorithm for compatibility with users of previous versions or certain legacy system environments.

### Time Offset

Allows the user to manually apply a fixed time offset (usually in seconds or milliseconds).

- Positive value: Local time runs **ahead** of network time (fast by X seconds/ms)  
- Negative value: Local time runs **behind** network time (slow by X seconds/ms)

Common use cases include testing, compensating for consistent network delays, or simulating specific clock behaviors.

### Error Tolerance (Sync Threshold)

If the difference between network time and local time is **less than** this threshold, no synchronization action will be performed.

- Recommended value: 45 ms  
- Purpose: Prevents unnecessary frequent adjustments to the system clock caused by minor network jitter or small drifts.

...

### By lalaki.cn
