package com.example.ezacrosscontrol

data class ConnectionReadiness(
    val developerEnabled: Boolean? = null,
    val usbEnabled: Boolean? = null,
    val wifiEnabled: Boolean? = null,
    val hostAvailable: Boolean = false,
    val usbConnected: Boolean = false,
    val wifiConnected: Boolean = false,
) {
    val usbReady get() = developerEnabled != false && usbEnabled != false && hostAvailable && usbConnected
    val wifiReady get() = developerEnabled != false && wifiEnabled != false && hostAvailable && wifiConnected
    fun transport(mode: String): String? = when (mode) {
        "USB" -> if (usbReady) "USB" else null
        "Wi-Fi" -> if (wifiReady) "Wi-Fi" else null
        else -> if (wifiReady) "Wi-Fi" else if (usbReady) "USB" else null
    }
    fun message(mode: String): String {
        if (developerEnabled == false) return "Ative as Opções do Desenvolvedor"
        transport(mode)?.let { return "Pronto para Conectar via $it" }
        if (mode == "USB" && usbEnabled == false) return "Ative a Depuração USB"
        if (mode == "Wi-Fi" && wifiEnabled == false) return "Ative a Depuração Wi-Fi"
        if (mode == "Auto" && usbEnabled == false && wifiEnabled == false) return "Ative a Depuração Wi-Fi ou USB"
        if (!hostAvailable) return "Abra o Host PC e verifique a conexão LAN"
        return when (mode) {
            "USB" -> "Conecte o cabo USB e autorize a depuração"
            "Wi-Fi" -> "Conecte e autorize a Depuração Wi-Fi no Host PC"
            else -> "Conecte e autorize a Depuração Wi-Fi ou USB"
        }
    }
}
