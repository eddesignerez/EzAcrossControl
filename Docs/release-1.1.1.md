# Windows 1.1.1

- Custom server ports request Windows elevation when the HTTP listening permission or matching private LAN firewall rule is missing. Cancellation keeps the server stopped.
- The selected port is saved after startup succeeds. Invalid and reserved ports are rejected.
- The active start button displays a translated “Started” in green.
- The main “Running” heading keeps its original text color.
- The Windows executable is named EzAcrossControl.exe; upgrades migrate shortcuts and preserve the selected firewall port.
- “Local server” shows its state separately: off in red, waiting in yellow, connected in green. Connected requires the Android companion HELLO; disconnection restores waiting.
- All eleven supported interface languages include these states.
- Restarting the listener cancels the old accept/writer loops using their original tokens.
- Uninstallation removes URL reservations recorded as owned by this application, including custom ports.

The Android production APK remains version 1.1.0 and is available in [release v1.1.0](https://github.com/eddesignerez/EzAcrossControl/releases/tag/v1.1.0). The unchanged native dependencies, corresponding source archive and dependency manifest are also available there.

Windows installers are unsigned. The firewall rule applies to private networks only; both devices need matching server ports.
