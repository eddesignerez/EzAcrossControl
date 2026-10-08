package com.example.ezacrosscontrol

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.core.view.WindowCompat
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.example.ezacrosscontrol.data.AppTheme
import com.example.ezacrosscontrol.data.PreferencesManager
import com.example.ezacrosscontrol.theme.EZAcrossControlTheme
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

import com.example.ezacrosscontrol.protocol.InputSessionManager
import com.example.ezacrosscontrol.control.AndroidControlManager
import com.example.ezacrosscontrol.control.ControlState
import android.provider.Settings
import android.content.Intent

class MainActivity : ComponentActivity() {
    private lateinit var webSocketClient: WebSocketClient
    private val sessionManager = InputSessionManager()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        webSocketClient = WebSocketClient(applicationContext)
        enableEdgeToEdge()
        val prefsManager = PreferencesManager(this)

        setContent {
            val theme by prefsManager.themeFlow.collectAsState(initial = AppTheme.System)
            val isDark = when (theme) {
                AppTheme.Dark -> true
                AppTheme.Light -> false
                AppTheme.System -> isSystemInDarkTheme()
            }

            SideEffect {
                val bars = WindowCompat.getInsetsController(window, window.decorView)
                bars.isAppearanceLightStatusBars = !isDark
                bars.isAppearanceLightNavigationBars = !isDark
            }

            EZAcrossControlTheme(darkTheme = isDark) {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    MainScreen(webSocketClient, prefsManager, sessionManager)
                }
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        webSocketClient.disconnect()
    }
}

@Composable
fun MainScreen(webSocketClient: WebSocketClient, prefsManager: PreferencesManager, sessionManager: InputSessionManager) {
    val coroutineScope = rememberCoroutineScope()
    val context = LocalContext.current

    val savedIp by prefsManager.ipFlow.collectAsState(initial = "")
    val savedPort by prefsManager.portFlow.collectAsState(initial = Config.DEFAULT_PORT)
    val savedTheme by prefsManager.themeFlow.collectAsState(initial = AppTheme.System)

    var ip by remember(savedIp) { mutableStateOf(savedIp.ifEmpty { "192.168." }) }
    var port by remember(savedPort) { mutableStateOf(savedPort) }
    var isConnected by remember { mutableStateOf(false) }
    var isConnecting by remember { mutableStateOf(false) }
    var connectionProgress by remember { mutableStateOf(0f) }
    var controlTransport by remember { mutableStateOf<String?>(null) }
    var status by remember { mutableStateOf("") }
    var accessibilityEnabled by remember { mutableStateOf(AndroidControlManager.isAccessibilityEnabled) }
    var controlState by remember { mutableStateOf(AndroidControlManager.currentState) }
    var controlStopped by remember { mutableStateOf(AndroidControlManager.isManuallyStopped) }
    var usbDebugEnabled by remember { mutableStateOf<Boolean?>(null) }
    var wifiDebugEnabled by remember { mutableStateOf<Boolean?>(null) }
    var latency by remember { mutableStateOf<Long?>(null) }
    var logText by remember { mutableStateOf("") }

    var lastEvent by remember { mutableStateOf(sessionManager.lastEvent) }
    var lastSequence by remember { mutableStateOf(sessionManager.lastSequence) }
    var currentHz by remember { mutableStateOf(sessionManager.currentHz) }

    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner) {
        fun debugEnabled(key: String): Boolean? = try {
            Settings.Global.getInt(context.contentResolver, key) == 1
        } catch (_: Settings.SettingNotFoundException) {
            null
        } catch (_: SecurityException) {
            null
        }
        fun refreshReadiness() {
            accessibilityEnabled = AndroidControlManager.isAccessibilityEnabled
            controlState = AndroidControlManager.currentState
            controlStopped = AndroidControlManager.isManuallyStopped
            usbDebugEnabled = debugEnabled(Settings.Global.ADB_ENABLED)
            wifiDebugEnabled = debugEnabled("adb_wifi_enabled")
        }
        refreshReadiness()
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) refreshReadiness()
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    LaunchedEffect(Unit) {
        webSocketClient.onConnectionStateChanged = { connected ->
            coroutineScope.launch {
            isConnected = connected
            isConnecting = false
            connectionProgress = if (connected) 1f else 0f
            status = ""
            AndroidControlManager.onConnectionStateChanged(connected)
            if (!connected) {
                controlTransport = null
                sessionManager.reset()
                lastEvent = sessionManager.lastEvent
                lastSequence = sessionManager.lastSequence
                currentHz = sessionManager.currentHz
                latency = null
            }
            }
        }
        webSocketClient.onConnectionProgressChanged = { progress ->
            coroutineScope.launch {
                isConnecting = true
                connectionProgress = progress
            }
        }
        webSocketClient.onConnectionFailure = { message ->
            coroutineScope.launch {
                isConnecting = false
                status = message
            }
        }
        webSocketClient.onLatencyUpdated = { l ->
            latency = l
        }
        webSocketClient.onLogMessage = { msg ->
            logText = "[$msg]\n$logText".take(8000)
        }
        webSocketClient.onEventReceived = { env ->
            if (env.type == "SESSION_STATUS") {
                coroutineScope.launch {
                    controlTransport = env.payload.optString("ControlTransport").takeIf { it == "USB" || it == "Wi-Fi" }
                }
            }
            sessionManager.onEventReceived(env)
        }

        sessionManager.onMouseMove = { nx, ny, dx, dy -> AndroidControlManager.handleMouseMoveWithState(nx, ny, dx, dy) }
        sessionManager.onMouseButton = { btn, act -> AndroidControlManager.handleMouseButtonWithState(btn, act) }
        sessionManager.onMouseWheel = { dx, dy -> AndroidControlManager.handleMouseWheel(dx, dy) }
        sessionManager.onTextCommit = { text -> AndroidControlManager.handleTextCommit(text) }
        sessionManager.onKeyDown = { vkCode -> AndroidControlManager.handleKeyDown(vkCode) }
        sessionManager.onKeyUp = { vkCode -> AndroidControlManager.handleKeyUp(vkCode) }
        sessionManager.onHandoffBegin = { edge, sessionId, clientTxTimestamp -> AndroidControlManager.handleHandoffBegin(edge, sessionId, clientTxTimestamp) }
        sessionManager.onHandoffEnd = { sessionId -> AndroidControlManager.handleHandoffEnd(sessionId) }
        sessionManager.onHandoffCancel = { AndroidControlManager.handleHandoffEnd(-1) }
    }

    LaunchedEffect(Unit) {
        AndroidControlManager.onStateChanged = { newState ->
            coroutineScope.launch {
            controlState = newState
            accessibilityEnabled = AndroidControlManager.isAccessibilityEnabled
            controlStopped = AndroidControlManager.isManuallyStopped
            }
        }
        AndroidControlManager.onRequestHandoffEnd = {
            val msg = org.json.JSONObject().apply {
                put("Type", "INPUT_HANDOFF_END")
                put("ProtocolVersion", 1)
                put("Payload", org.json.JSONObject())
            }.toString()
            webSocketClient.sendMessage(msg)
        }
        AndroidControlManager.onRequestReturnToWindows = { sessionId, edge ->
            val msg = org.json.JSONObject().apply {
                put("Type", "RETURN_TO_WINDOWS")
                put("ProtocolVersion", 1)
                put("Payload", org.json.JSONObject().apply {
                    put("SessionId", sessionId)
                    put("Edge", edge)
                    put("Timestamp", System.currentTimeMillis())
                })
            }.toString()
            webSocketClient.sendMessage(msg)
        }
    }

    // Ping loop
    LaunchedEffect(isConnected) {
        if (isConnected) {
            while (true) {
                delay(2000)
                webSocketClient.sendPing()
            }
        }
    }

    // UI Throttled Update Loop for Input Session Debug
    LaunchedEffect(isConnected) {
        if (isConnected) {
            while (true) {
                delay(250)
                lastEvent = sessionManager.lastEvent
                lastSequence = sessionManager.lastSequence
                currentHz = sessionManager.currentHz
            }
        }
    }

    DashboardScreen(
        ip = ip,
        port = port,
        theme = savedTheme,
        isConnected = isConnected,
        isConnecting = isConnecting,
        connectionProgress = connectionProgress,
        controlTransport = controlTransport,
        status = status,
        latency = latency,
        controlState = controlState,
        accessibilityEnabled = accessibilityEnabled,
        controlStopped = controlStopped,
        usbDebugEnabled = usbDebugEnabled,
        wifiDebugEnabled = wifiDebugEnabled,
        lastEvent = lastEvent,
        lastSequence = lastSequence,
        currentHz = currentHz,
        logText = logText,
        onIpChange = { value ->
            ip = value
            coroutineScope.launch { prefsManager.setIp(value) }
        },
        onPortChange = { value ->
            port = value
            coroutineScope.launch { prefsManager.setPort(value) }
        },
        onThemeChange = { value -> coroutineScope.launch { prefsManager.setTheme(value) } },
        onConnect = {
            status = ""
            webSocketClient.connect(ip, port)
        },
        onDisconnect = { webSocketClient.disconnect() },
        onOpenAccessibility = { context.startActivity(Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS)) },
        onOpenDeveloperSettings = { context.startActivity(Intent(Settings.ACTION_APPLICATION_DEVELOPMENT_SETTINGS)) },
        onStopControl = { AndroidControlManager.stopRemoteControl() },
        onEnableIme = { context.startActivity(Intent(Settings.ACTION_INPUT_METHOD_SETTINGS)) },
        onSelectIme = {
            val inputMethodManager = context.getSystemService(android.content.Context.INPUT_METHOD_SERVICE) as android.view.inputmethod.InputMethodManager
            inputMethodManager.showInputMethodPicker()
        },
    )
}
