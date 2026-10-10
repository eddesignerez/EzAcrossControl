# EzAcrossControl

**Conecta. Controla. Continúa.** · by ElementZero

[Todos los idiomas y guía técnica completa (portugués)](../../README.md)

Controla tu dispositivo Android con el ratón y el teclado de Windows. Lleva el puntero al borde de pantalla seleccionado para cambiar de dispositivo. Las aplicaciones se comunican por la red local, sin cuenta ni enrutamiento en la nube.

## Descargar la versión Windows 1.1.4 / Android 1.1.7

- [Instalador de Windows](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [APK de Android](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.7/EZAcrossControl-1.1.7-android.apk)
- [ZIP portátil para Windows](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [Versiones y comprobaciones de integridad](https://github.com/eddesignerez/EzAcrossControl/releases)

## Requisitos y primera conexión

Se necesitan Windows 10/11 de 64 bits, Android 7 o posterior y ambos equipos en la misma red local, incluso si se usa USB para el control nativo. Autoriza la depuración USB o empareja la depuración inalámbrica (Android 11 o posterior). El control nativo depende de la compatibilidad UHID del dispositivo.

1. Instala y abre EzAcrossControl en Windows. El instalador solicita permiso de administrador para configurar el servidor local y el cortafuegos de la red privada.
2. Instala el APK. En Android, abre **Modo avanzado → Depuración → Abrir configuración**, activa las opciones de desarrollador y el método de depuración, y autoriza el ordenador.
3. Para la depuración inalámbrica, empareja el dispositivo con el ADB incluido según la [guía detallada](../../README.md#primeiro-uso). El emparejamiento y la conexión usan puertos distintos.
4. En el APK, elige **Auto**, **Wi-Fi** o **USB**. Introduce la IP mostrada en Windows y el mismo puerto en ambas aplicaciones (predeterminado: **8765**). Pulsa **Conectar** y selecciona el borde de Android en Windows.

Android usa el idioma del sistema por defecto. Puedes cambiarlo en **Modo avanzado → Idioma**; en Windows, el selector está en Diagnóstico.

## Vistas previas y límites

![Propuesta estática Day](../../Brand/Assets/PNG/splash-day-1600x900.png)
![Propuesta estática Night](../../Brand/Assets/PNG/splash-night-1600x900.png)

Son **propuestas visuales estáticas, no capturas de la aplicación en funcionamiento**. La tipografía y la armonización de colores aún esperan aprobación. Las pruebas físicas se limitaron a HiPadPlus y LAVIE T11; otros dispositivos requieren comprobación. El instalador de Windows no tiene firma Authenticode. Consulta la [guía de solución de problemas y arquitectura](../../README.md#solução-de-problemas) y los [avisos de terceros](../../THIRD-PARTY-NOTICES.md).
