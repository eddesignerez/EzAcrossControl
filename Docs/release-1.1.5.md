# Android 1.1.5

- Adds the supplied monochrome connection artwork to the Android notification/status bar while the Host connection is confirmed.
- Shows a silent ongoing notification with the selected language and actual USB or Wi-Fi control transport. Tapping it returns to the current app screen.
- Removes the indicator when disconnected or when the activity/session is destroyed.
- Requests notification permission on Android 13 and newer when connecting. A denied permission does not block the Host connection.

Installed with data preserved on HiPadPlus (Android 11). Verified real Host connection over USB, icon rendering, notification text, removal on disconnect and reappearance on reconnect. Three focused notification tests pass, including denied permission on API 33.

The current Windows release remains [v1.1.4](https://github.com/eddesignerez/EzAcrossControl/releases/tag/v1.1.4). Native dependency sources remain available in [v1.1.2](https://github.com/eddesignerez/EzAcrossControl/releases/tag/v1.1.2).