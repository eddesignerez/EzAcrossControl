# EzAcrossControl

**Connect. Control. Continue.** · by ElementZero

[All languages and full technical guide (Portuguese)](../../README.md)

Control your Android device with the Windows mouse and keyboard. Move the pointer across the selected screen edge to switch control. The apps communicate over your local network, with no account or cloud routing.

## Download version 1.1.2

- [Windows installer](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.2/EZAcrossControl-1.1.2-windows-x64-setup.exe)
- [Android APK](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.2/EZAcrossControl-1.1.2-android.apk)
- [Portable Windows ZIP](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.2/EZAcrossControl-1.1.2-windows-x64-portable.zip)
- [Releases and integrity checks](https://github.com/eddesignerez/EzAcrossControl/releases)

## Requirements and first connection

Windows 10/11 (64-bit), Android 7 or later, and both devices on the same LAN are required, even when USB is selected for native control. Authorize USB debugging or pair wireless debugging (Android 11 or later). Native control depends on the device's UHID support.

1. Install and open EzAcrossControl on Windows. The installer requests administrator permission to configure the local server and private-network firewall.
2. Install the APK. On Android, open **Advanced Mode → Debugging → Open Settings**, enable developer options and the desired debugging method, then authorize the computer.
3. For wireless debugging, pair the device with the bundled ADB as described in the [detailed guide](../../README.md#primeiro-uso). Pairing and connection use different ports.
4. In the APK, choose **Auto**, **Wi-Fi**, or **USB**. Enter the host IP shown on Windows and the same port in both apps (default **8765**). Tap **Connect** and select the Android screen edge on Windows.

Android normally follows the system language. Change it under **Advanced Mode → Language**; the Windows language selector is in Diagnostics.

## Visual previews and limits

![Static Day brand proposal](../../Brand/Assets/PNG/splash-day-1600x900.png)
![Static Night brand proposal](../../Brand/Assets/PNG/splash-night-1600x900.png)

These are **static brand proposals, not screenshots of the running app**. Typography and color harmonization still await approval. Physical validation was limited to HiPadPlus and LAVIE T11; other devices need on-device checks. The Windows installer is not Authenticode-signed. See the [Portuguese troubleshooting and architecture guide](../../README.md#solução-de-problemas) and [third-party notices](../../THIRD-PARTY-NOTICES.md).
