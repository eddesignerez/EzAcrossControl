# EzAcrossControl

**Verbinden. Steuern. Weiterarbeiten.** · by ElementZero

[Alle Sprachen und vollständige technische Anleitung (Portugiesisch)](../../README.md)

Steuern Sie Ihr Android-Gerät mit Maus und Tastatur von Windows. Bewegen Sie den Zeiger an den gewählten Bildschirmrand, um die Steuerung zu wechseln. Die Apps kommunizieren über das lokale Netzwerk, ohne Konto oder Cloud-Routing.

## Version Windows 1.1.4 / Android 1.1.7 herunterladen

- [Windows-Installationsprogramm](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [Android-APK](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.7/EZAcrossControl-1.1.7-android.apk)
- [Portables Windows-ZIP](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [Releases und Integritätsprüfungen](https://github.com/eddesignerez/EzAcrossControl/releases)

## Voraussetzungen und erste Verbindung

Erforderlich sind Windows 10/11 (64 Bit), Android 7 oder höher und beide Geräte im selben LAN. Das gilt auch, wenn USB für die native Steuerung gewählt wird. Erlauben Sie USB-Debugging oder koppeln Sie kabelloses Debugging (ab Android 11). Die native Steuerung hängt von der UHID-Unterstützung des Geräts ab.

1. Installieren und öffnen Sie EzAcrossControl unter Windows. Das Installationsprogramm fordert Administratorrechte an, um den lokalen Server und die Firewall für private Netzwerke einzurichten.
2. Installieren Sie die APK. Öffnen Sie auf Android **Erweiterter Modus → Debugging → Einstellungen öffnen**, aktivieren Sie Entwickleroptionen und das gewünschte Debugging und autorisieren Sie den Computer.
3. Für kabelloses Debugging koppeln Sie das Gerät mit dem mitgelieferten ADB gemäß der [ausführlichen Anleitung](../../README.md#primeiro-uso). Kopplung und Verbindung verwenden unterschiedliche Ports.
4. Wählen Sie in der APK **Auto**, **Wi-Fi** oder **USB**. Geben Sie die unter Windows angezeigte Host-IP und in beiden Apps denselben Port ein (Standard: **8765**). Tippen Sie auf **Verbinden** und wählen Sie unter Windows den Android-Bildschirmrand.

Android verwendet normalerweise die Systemsprache. Ändern Sie sie unter **Erweiterter Modus → Sprache**; unter Windows befindet sich die Auswahl in der Diagnose.

## Visuelle Vorschau und Grenzen

![Statischer Day-Entwurf](../../Brand/Assets/PNG/splash-day-1600x900.png)
![Statischer Night-Entwurf](../../Brand/Assets/PNG/splash-night-1600x900.png)

Dies sind **statische Markenvorschläge, keine Screenshots der laufenden App**. Typografie und Farbabstimmung müssen noch freigegeben werden. Physische Tests beschränkten sich auf HiPadPlus und LAVIE T11; andere Geräte müssen einzeln geprüft werden. Das Windows-Installationsprogramm ist nicht mit Authenticode signiert. Siehe [Fehlerbehebung und Architektur](../../README.md#solução-de-problemas) sowie [Hinweise zu Drittanbietern](../../THIRD-PARTY-NOTICES.md).
