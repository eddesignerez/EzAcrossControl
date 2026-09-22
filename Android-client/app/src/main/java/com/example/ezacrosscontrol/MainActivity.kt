package com.example.ezacrosscontrol

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
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
    private val webSocketClient = WebSocketClient()
    private val sessionManager = InputSessionManager()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val prefsManager = PreferencesManager(this)

        setContent {
            val theme by prefsManager.themeFlow.collectAsState(initial = AppTheme.System)
            val isDark = when (theme) {
                AppTheme.Dark -> true
                AppTheme.Light -> false
                AppTheme.System -> isSystemInDarkTheme()
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

@OptIn(ExperimentalMaterial3Api::class)
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
    var status by remember { mutableStateOf("Disconnected") }
    var latency by remember { mutableStateOf<Long?>(null) }
    var logText by remember { mutableStateOf("") }
    
    var lastEvent by remember { mutableStateOf(sessionManager.lastEvent) }
    var lastSequence by remember { mutableStateOf(sessionManager.lastSequence) }
    var currentHz by remember { mutableStateOf(sessionManager.currentHz) }
    
    var themeDropdownExpanded by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        webSocketClient.onConnectionStateChanged = { connected ->
            isConnected = connected
            status = if (connected) "Connected" else "Disconnected"
            AndroidControlManager.onConnectionStateChanged(connected)
            if (!connected) {
                sessionManager.reset()
                lastEvent = sessionManager.lastEvent
                lastSequence = sessionManager.lastSequence
                currentHz = sessionManager.currentHz
                latency = null
            }
        }
        webSocketClient.onLatencyUpdated = { l ->
            latency = l
        }
        webSocketClient.onLogMessage = { msg ->
            logText = "[$msg]\n$logText"
        }
        webSocketClient.onEventReceived = { env ->
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

    var controlState by remember { mutableStateOf(AndroidControlManager.currentState) }
    LaunchedEffect(Unit) {
        AndroidControlManager.onStateChanged = { newState ->
            controlState = newState
        }
        AndroidControlManager.onRequestHandoffEnd = {
            val msg = org.json.JSONObject().apply {
                put("Type", "INPUT_HANDOFF_END")
                put("ProtocolVersion", 1)
                put("Payload", org.json.JSONObject())
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

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp, 48.dp, 16.dp, 16.dp)
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(
                text = "EZ Across Control",
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.primary,
                modifier = Modifier.padding(bottom = 16.dp)
            )

            ExposedDropdownMenuBox(
                expanded = themeDropdownExpanded,
                onExpandedChange = { themeDropdownExpanded = it }
            ) {
                OutlinedTextField(
                    value = savedTheme.name,
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("Theme") },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = themeDropdownExpanded) },
                    modifier = Modifier.menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable).width(120.dp)
                )
                ExposedDropdownMenu(
                    expanded = themeDropdownExpanded,
                    onDismissRequest = { themeDropdownExpanded = false }
                ) {
                    AppTheme.entries.forEach { t ->
                        DropdownMenuItem(
                            text = { Text(t.name) },
                            onClick = {
                                coroutineScope.launch { prefsManager.setTheme(t) }
                                themeDropdownExpanded = false
                            }
                        )
                    }
                }
            }
        }

        OutlinedTextField(
            value = ip,
            onValueChange = { 
                ip = it
                coroutineScope.launch { prefsManager.setIp(it) }
            },
            label = { Text("PC IP Address") },
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(8.dp))

        OutlinedTextField(
            value = port,
            onValueChange = { 
                port = it
                coroutineScope.launch { prefsManager.setPort(it) }
            },
            label = { Text("Port") },
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(16.dp))

        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
            Button(
                onClick = { webSocketClient.connect(ip, port) },
                enabled = !isConnected,
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
            ) {
                Text("Connect")
            }

            Button(
                onClick = { webSocketClient.disconnect() },
                enabled = isConnected,
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.outline, contentColor = MaterialTheme.colorScheme.onSurface)
            ) {
                Text("Disconnect")
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        Surface(
            color = MaterialTheme.colorScheme.surfaceVariant,
            shape = MaterialTheme.shapes.medium,
            modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = "Status: $status", style = MaterialTheme.typography.bodyLarge)
                Text(text = "Latency: ${latency?.let { "${it}ms" } ?: "-"}", style = MaterialTheme.typography.bodyLarge)
            }
        }

        Surface(
            color = MaterialTheme.colorScheme.surfaceVariant,
            shape = MaterialTheme.shapes.medium,
            modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = "Input Session Debug", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                Text(text = "Last Event: $lastEvent", style = MaterialTheme.typography.bodyLarge)
                Text(text = "Sequence: $lastSequence", style = MaterialTheme.typography.bodyLarge)
                Text(text = "Rate: $currentHz ev/s", style = MaterialTheme.typography.bodyLarge)
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        Surface(
            color = MaterialTheme.colorScheme.surfaceVariant,
            shape = MaterialTheme.shapes.medium,
            modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = "Remote Control", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                
                // We'll check if the service is alive by looking at controlState or directly checking accessibility 
                // But for now, controlState > Disabled means it's Enabled.
                val a11yStatus = if (controlState != ControlState.Disabled) "Enabled" else "Disabled"
                
                Text(text = "Accessibility: $a11yStatus", style = MaterialTheme.typography.bodyLarge)
                Text(text = "Remote Control: ${controlState.name}", style = MaterialTheme.typography.bodyLarge)
                
                Spacer(modifier = Modifier.height(8.dp))
                
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Button(onClick = {
                        val intent = Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS)
                        context.startActivity(intent)
                    }) {
                        Text("Open Settings")
                    }
                    Button(
                        onClick = { AndroidControlManager.stopRemoteControl() },
                        enabled = controlState == ControlState.Controlling || controlState == ControlState.Ready,
                        colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                    ) {
                        Text("Stop Control")
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        Surface(
            color = MaterialTheme.colorScheme.surfaceVariant,
            shape = MaterialTheme.shapes.medium,
            modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = "Remote Keyboard", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                
                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
                    Text(text = "Status: API >= 33 (Automatic Ownership)", style = MaterialTheme.typography.bodyLarge)
                    Text(text = "No manual IME switching required.", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                } else {
                    val imeReady = com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService.isReady()
                    val imeStatus = if (imeReady) "Enabled & Selected" else "Not Selected"
                    
                    Text(text = "Status: $imeStatus", style = MaterialTheme.typography.bodyLarge)
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Button(onClick = {
                            val intent = Intent(Settings.ACTION_INPUT_METHOD_SETTINGS)
                            context.startActivity(intent)
                        }) {
                            Text("Enable IME")
                        }
                        
                        val inputMethodManager = context.getSystemService(android.content.Context.INPUT_METHOD_SERVICE) as android.view.inputmethod.InputMethodManager
                        Button(onClick = {
                            inputMethodManager.showInputMethodPicker()
                        }) {
                            Text("Select IME")
                        }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        Text(text = "Log:", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        HorizontalDivider()
        Text(
            text = logText,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier
                .fillMaxSize()
                .padding(top = 8.dp)
        )
    }
}
