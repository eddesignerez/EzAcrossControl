# EzAcrossControl

**连接。控制。继续。** · by ElementZero

[全部语言与完整技术指南（葡萄牙语）](../../README.md)

使用 Windows 的鼠标和键盘控制 Android 设备。将指针移到选定的屏幕边缘，即可切换控制。两个应用通过局域网通信，无需账号，也不通过云端转发。

## 下载 Windows 1.1.4 / Android 1.1.5 版

- [Windows 安装程序](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [Android APK](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.5/EZAcrossControl-1.1.5-android.apk)
- [Windows 便携版 ZIP](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [发布版本与完整性校验](https://github.com/eddesignerez/EzAcrossControl/releases)

## 使用条件与首次连接

需要 64 位 Windows 10/11、Android 7 或更高版本，且两台设备处于同一局域网。即使用 USB 进行原生控制，局域网仍然必需。请授权 USB 调试，或配对无线调试（Android 11 或更高版本）。原生控制取决于设备是否支持 UHID。

1. 在 Windows 上安装并打开 EzAcrossControl。安装程序会请求管理员权限，以设置本地服务器和专用网络防火墙。
2. 安装 APK。在 Android 中打开**高级模式 → 调试 → 打开设置**，启用开发者选项和所需的调试方式，并授权这台电脑。
3. 使用无线调试时，按照[详细指南](../../README.md#primeiro-uso)使用随附的 ADB 配对设备。配对端口与连接端口不同。
4. 在 APK 中选择 **Auto**、**Wi-Fi** 或 **USB**。输入 Windows 显示的主机 IP，并在两个应用中设置相同端口（默认 **8765**）。点击**连接**，然后在 Windows 中选择 Android 对应的屏幕边缘。

Android 默认跟随系统语言，可在**高级模式 → 语言**中更改；Windows 的语言选择位于诊断页面。

## 视觉预览与限制

![静态 Day 品牌提案](../../Brand/Assets/PNG/splash-day-1600x900.png)
![静态 Night 品牌提案](../../Brand/Assets/PNG/splash-night-1600x900.png)

这些是**静态品牌提案，并非应用运行时的截图**。字体与配色协调方案尚待批准。实机验证仅覆盖 HiPadPlus 和 LAVIE T11；其他设备需要自行验证。Windows 安装程序尚未使用 Authenticode 签名。另请参阅[故障排查与架构](../../README.md#solução-de-problemas)及[第三方声明](../../THIRD-PARTY-NOTICES.md)。
