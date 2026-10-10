# EzAcrossControl

**Connetti. Controlla. Continua.** · by ElementZero

[Tutte le lingue e guida tecnica completa (portoghese)](../../README.md)

Controlla il dispositivo Android con mouse e tastiera di Windows. Sposta il puntatore verso il bordo dello schermo scelto per passare da un dispositivo all'altro. Le app comunicano sulla rete locale, senza account né instradamento cloud.

## Scarica la versione Windows 1.1.4 / Android 1.1.6

- [Programma di installazione Windows](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [APK Android](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.6/EZAcrossControl-1.1.6-android.apk)
- [ZIP portatile Windows](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [Release e verifiche di integrità](https://github.com/eddesignerez/EzAcrossControl/releases)

## Requisiti e primo collegamento

Servono Windows 10/11 a 64 bit, Android 7 o successivo e i due dispositivi sulla stessa LAN, anche se si usa USB per il controllo nativo. Autorizza il debug USB oppure associa il debug wireless (Android 11 o successivo). Il controllo nativo dipende dal supporto UHID del dispositivo.

1. Installa e apri EzAcrossControl su Windows. Il programma di installazione richiede i privilegi di amministratore per configurare il server locale e il firewall della rete privata.
2. Installa l'APK. Su Android apri **Modalità avanzata → Debug → Apri impostazioni**, abilita le opzioni sviluppatore e il metodo di debug, quindi autorizza il computer.
3. Per il debug wireless, associa il dispositivo con l'ADB incluso seguendo la [guida dettagliata](../../README.md#primeiro-uso). Le porte di associazione e connessione sono diverse.
4. Nell'APK scegli **Auto**, **Wi-Fi** o **USB**. Inserisci l'IP mostrato su Windows e la stessa porta in entrambe le app (predefinita: **8765**). Tocca **Connetti** e scegli il bordo dello schermo Android su Windows.

Android segue normalmente la lingua di sistema. Puoi cambiarla in **Modalità avanzata → Lingua**; su Windows il selettore è in Diagnostica.

## Anteprime e limiti

![Proposta statica Day](../../Brand/Assets/PNG/splash-day-1600x900.png)
![Proposta statica Night](../../Brand/Assets/PNG/splash-night-1600x900.png)

Queste sono **proposte grafiche statiche, non schermate dell'app in esecuzione**. Tipografia e armonizzazione dei colori sono ancora da approvare. I test fisici hanno riguardato solo HiPadPlus e LAVIE T11; altri dispositivi richiedono verifiche specifiche. Il programma di installazione Windows non è firmato con Authenticode. Consulta la [guida per problemi e architettura](../../README.md#solução-de-problemas) e le [note sulle licenze di terzi](../../THIRD-PARTY-NOTICES.md).
