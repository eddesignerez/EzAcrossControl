package com.example.ezacrosscontrol

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.animation.animateContentSize
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.progressBarRangeInfo
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.ezacrosscontrol.control.ControlState
import com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService
import com.example.ezacrosscontrol.data.AppTheme
import com.example.ezacrosscontrol.theme.FrauncesBrand
import kotlin.math.cos
import kotlin.math.roundToInt
import kotlin.math.sin

@Composable
internal fun DashboardScreen(
    ip: String, port: String, theme: AppTheme,
    language: String, onLanguageChange: (String) -> Unit,
    isConnected: Boolean, isConnecting: Boolean, connectionProgress: Float,
    controlTransport: String?, status: String, latency: Long?,
    controlState: ControlState, accessibilityEnabled: Boolean, controlStopped: Boolean,
    readiness: ConnectionReadiness, connectionMode: String,
    keyboardEnabled: Boolean, keyboardSelected: Boolean,
    onConnectionModeChange: (String) -> Unit,
    lastEvent: String, lastSequence: Long, currentHz: Int, logText: String,
    onIpChange: (String) -> Unit, onPortChange: (String) -> Unit,
    onThemeChange: (AppTheme) -> Unit, onConnect: () -> Unit, onDisconnect: () -> Unit,
    onOpenAccessibility: () -> Unit, onOpenDeveloperSettings: () -> Unit,
    onStopControl: () -> Unit, onEnableIme: () -> Unit, onSelectIme: () -> Unit,
) {
    val strings = LocalStrings.current
    var advancedOpen by remember { mutableStateOf(false) }
    var diagnosticsOpen by remember { mutableStateOf(false) }

    val colors = MaterialTheme.colorScheme
    val corner = RoundedCornerShape(10.dp)

    // Leave the real Android status bar, cutouts and navigation area clear.
    BoxWithConstraints(Modifier.fillMaxSize().safeDrawingPadding(), contentAlignment = Alignment.TopCenter) {
        val compact = maxWidth < 400.dp
        val tablet = maxWidth >= 600.dp
        Column(
            Modifier.fillMaxWidth()
                .verticalScroll(rememberScrollState()).padding(if (tablet) 24.dp else 16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Row(Modifier.fillMaxWidth().padding(vertical = 8.dp),
                horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                Image(painterResource(R.drawable.ez_across_logo), "Logo EZ Across Control",
                    Modifier.size(44.dp).clip(RoundedCornerShape(10.dp)))
                Spacer(Modifier.width(10.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(strings["EZ"], Modifier.alignByBaseline(), fontFamily = FrauncesBrand,
                        fontWeight = FontWeight.ExtraBold, fontSize = if (compact) 24.sp else 28.sp, maxLines = 1)
                    Text(strings[" Across Control"], Modifier.alignByBaseline(), fontFamily = FrauncesBrand,
                        fontWeight = FontWeight.Medium, fontStyle = FontStyle.Italic,
                        fontSize = if (compact) 22.sp else 26.sp, maxLines = 1)
                }
            }
            DashboardCard {
                Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                    Text(strings["CONTROLE ENTRE TELAS"], Modifier.fillMaxWidth().padding(horizontal = 40.dp), textAlign = TextAlign.Center,
                        style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                    ThemeToggle(theme, onThemeChange, Modifier.align(Alignment.CenterEnd))
                }
                Spacer(Modifier.height(20.dp))
                val controlReady = isConnected && controlTransport != null && !controlStopped
                    && readiness.transport(controlTransport) != null
                val awaitingControl = isConnecting || (isConnected && !controlReady)
                ConnectionCircle(controlReady, awaitingControl, if (isConnected && !controlReady) .65f else connectionProgress, diameter = if (tablet) 280.dp else 224.dp,
                    onToggle = { if (isConnected || isConnecting) onDisconnect() else onConnect() })
                Spacer(Modifier.height(18.dp))
                Text(strings[when {
                    isConnected && status.isNotBlank() -> status
                    isConnected && !controlReady -> "Controle Indisponível. Verifique a Depuração."
                    isConnected -> controlTransport ?: "Aguardando Depuração no Host"
                    isConnecting -> "Aguardando Host"
                    status.isNotBlank() -> status
                    else -> readiness.message(connectionMode)
                }], Modifier.fillMaxWidth(), textAlign = TextAlign.Center,
                    style = MaterialTheme.typography.bodySmall,
                    color = if (controlReady || (!isConnected && !isConnecting && status.isBlank() && readiness.transport(connectionMode) != null)) colors.secondary
                        else if (isConnected) colors.tertiary else colors.onSurfaceVariant,
                    fontWeight = if (isConnected) FontWeight.SemiBold else FontWeight.Normal)
            }
            DashboardCard {
                CardHeading("Connection Mode")
                Spacer(Modifier.height(8.dp))
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    listOf("Auto", "Wi-Fi", "USB").forEach { mode ->
                        FilterChip(selected = connectionMode == mode,
                            onClick = { onConnectionModeChange(mode) },
                            enabled = !isConnected && !isConnecting,
                            label = { Text(strings[mode], Modifier.fillMaxWidth(), textAlign = TextAlign.Center) },
                            modifier = Modifier.weight(1f),
                            colors = FilterChipDefaults.filterChipColors(
                                containerColor = colors.surfaceVariant, labelColor = colors.onSurfaceVariant,
                                selectedContainerColor = colors.primaryContainer, selectedLabelColor = colors.primary,
                                disabledContainerColor = colors.surfaceVariant, disabledLabelColor = colors.onSurfaceVariant,
                                disabledSelectedContainerColor = colors.primaryContainer),
                            border = BorderStroke(1.dp, if (connectionMode == mode) colors.primary else colors.outline))
                    }
                }
                Text(strings["Auto: preferência Wi-Fi; USB quando disponível."], Modifier.fillMaxWidth(),
                    style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
            }
            DashboardCard {
                CardHeading("Host PC")
                Spacer(Modifier.height(8.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedTextField(ip, onIpChange, Modifier.weight(1f),
                        enabled = !isConnected && !isConnecting,
                        label = { Text(strings["Endereço do IP"]) }, singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri), shape = corner)
                    OutlinedTextField(port, onPortChange, Modifier.width(92.dp),
                        enabled = !isConnected && !isConnecting,
                        label = { Text(strings["Porta"]) }, singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), shape = corner)
                }
            }
            DashboardCard {
                Row(Modifier.fillMaxWidth().clickable(role = Role.Button,
                    onClickLabel = if (advancedOpen) "Recolher Modo Avançado" else "Abrir Modo Avançado",
                    onClick = { advancedOpen = !advancedOpen }).padding(vertical = 5.dp),
                    verticalAlignment = Alignment.CenterVertically) {
                    GearIcon()
                    Spacer(Modifier.width(9.dp))
                    Text(strings["Modo Avançado"], Modifier.weight(1f),
                        style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                    Text(strings[if (advancedOpen) "⌃" else "⌄"], color = colors.onSurfaceVariant)
                }
                Column(Modifier.animateContentSize()) {
                    if (advancedOpen) {
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        CardHeading("Depuração")
                        Spacer(Modifier.height(12.dp))
                        ReadinessStatus("USB:", if (readiness.usbReady) "Ready" else if (readiness.usbEnabled == false) "Disable" else "Waiting", readiness.usbReady, failed = readiness.usbEnabled == false, waiting = readiness.usbEnabled != false && !readiness.usbReady)
                        Spacer(Modifier.height(8.dp))
                        ReadinessStatus("Wi-Fi:", if (readiness.wifiReady) "Ready" else if (readiness.wifiEnabled == false) "Disable" else "Waiting", readiness.wifiReady, failed = readiness.wifiEnabled == false, waiting = readiness.wifiEnabled != false && !readiness.wifiReady)
                        Spacer(Modifier.height(12.dp))
                        OutlinedButton(onOpenDeveloperSettings, Modifier.fillMaxWidth(), shape = corner) { Text(strings["Open Settings"]) }
                        Text(strings["Opções do Desenvolvedor"], Modifier.fillMaxWidth(), textAlign = TextAlign.Center,
                            style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        CardHeading("Remote Control")
                        Spacer(Modifier.height(12.dp))
                        ReadinessStatus("Accessibility:", if (accessibilityEnabled) "Enable" else "Disable", accessibilityEnabled, failed = !accessibilityEnabled)
                        Spacer(Modifier.height(8.dp))
                        val ready = accessibilityEnabled && !controlStopped && controlState != ControlState.Disabled
                        ReadinessStatus("Remote Control:",
                            if (ready) "Ready" else "Waiting", ready, waiting = !ready)
                        Spacer(Modifier.height(12.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onOpenAccessibility, Modifier.weight(1f), shape = corner) { Text(strings["Open Settings"]) }
                            Button(onStopControl, Modifier.weight(1f), enabled = isConnected && !controlStopped,
                                shape = corner) { Text(strings["Stop Control"]) }
                        }
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        CardHeading("Remote Keyboard")
                        Spacer(Modifier.height(12.dp))
                        ReadinessStatus("Keyboard:", if (keyboardEnabled) "Enable" else "Disable", keyboardEnabled, failed = !keyboardEnabled)
                        Spacer(Modifier.height(8.dp))
                        ReadinessStatus("Keyboard:", if (keyboardSelected) "Selected" else "No Selected", keyboardSelected, failed = !keyboardSelected)
                        Spacer(Modifier.height(8.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onEnableIme, Modifier.weight(1f), shape = corner) { Text(strings["Open Settings"]) }
                            OutlinedButton(onSelectIme, Modifier.weight(1f), shape = corner) { Text(strings["Select Keyboard"]) }
                        }
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        LanguageSelector(language, onLanguageChange)
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        TextButton({ diagnosticsOpen = !diagnosticsOpen }, Modifier.fillMaxWidth()) { Text(strings["Diagnostics & Activity Log"]) }
                        if (diagnosticsOpen) {
                            ReadinessStatus("Latency:", latency?.let { "$it ms" } ?: "—", false)
                            ReadinessStatus("Last Event:", lastEvent, false)
                            ReadinessStatus("Sequence:", lastSequence.toString(), false)
                            ReadinessStatus("Rate:", "$currentHz ev/s", false)
                            Spacer(Modifier.height(8.dp))
                            Text(strings[logText.ifBlank { "No activity yet." }],
                                style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                        }
                    }
                }
            }


        }
    }
}

@Composable
private fun LanguageSelector(language: String, onChange: (String) -> Unit) {
    val strings = LocalStrings.current
    val context = LocalContext.current
    var expanded by remember { mutableStateOf(false) }
    val options = listOf(LanguageOption("system", strings["System Language"])) + strings.languages
    val version = remember(context) {
        context.packageManager.getPackageInfo(context.packageName, 0).versionName ?: "—"
    }
    CardHeading("Language")
    Spacer(Modifier.height(8.dp))
    Box(Modifier.fillMaxWidth()) {
        OutlinedButton(onClick = { expanded = true }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp)) {
            Text(options.firstOrNull { it.code == language }?.label ?: strings["System Language"], Modifier.weight(1f))
            Text("⌄")
        }
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            options.forEach { option -> DropdownMenuItem(text = { Text(option.label) }, onClick = {
                expanded = false
                onChange(option.code)
            }) }
        }
    }
    Spacer(Modifier.height(8.dp))
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(strings["Application Version"], style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text("v$version", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.SemiBold)
    }
}

@Composable
private fun ConnectionCircle(connected: Boolean, connecting: Boolean, progress: Float, diameter: androidx.compose.ui.unit.Dp, onToggle: () -> Unit) {
    val strings = LocalStrings.current
    val colors = MaterialTheme.colorScheme
    val animatedProgress by animateFloatAsState(
        if (connected) 1f else if (connecting) progress.coerceIn(0f, .95f) else 0f,
        tween(450), label = "Connection Progress")
    Box(Modifier.size(diameter).clip(CircleShape).semantics {
        stateDescription = strings[when { connected -> "Conectado"; connecting -> "Aguardando Host"; else -> "Desconectado" }]
        if (connecting) progressBarRangeInfo = ProgressBarRangeInfo(animatedProgress, 0f..1f)
    }.clickable(role = Role.Button, onClickLabel = strings[if (connected || connecting) "Desconectar" else "Conectar"],
        onClick = onToggle), contentAlignment = Alignment.Center) {
        Canvas(Modifier.fillMaxSize()) {
            val stroke = 12.dp.toPx()
            val radius = size.minDimension / 2 - 14.dp.toPx()
            val origin = Offset(center.x - radius, center.y - radius)
            val diameter = Size(radius * 2, radius * 2)
            drawCircle(colors.outline, radius, style = Stroke(stroke))
            if (animatedProgress > 0f) {
                drawArc(colors.primary, -90f, animatedProgress * 360f, false, origin, diameter,
                    style = Stroke(stroke, cap = StrokeCap.Round))
                if (animatedProgress < .999f && (connecting || connected)) {
                    val angle = Math.toRadians((-90 + animatedProgress * 360).toDouble())
                    val tip = Offset(center.x + cos(angle).toFloat() * radius, center.y + sin(angle).toFloat() * radius)
                    drawCircle(colors.surface, 10.dp.toPx(), tip)
                    drawCircle(colors.primary, 8.dp.toPx(), tip)
                }
            }
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(strings[when { connected -> "Conectado"; connecting -> "Aguardando"; else -> "Conectar" }],
                fontSize = 22.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center)
            Text(strings[when { connected -> "TOQUE PARA SAIR"; connecting -> "${(animatedProgress * 100).roundToInt()}%"; else -> "TOQUE AQUI" }],
                style = if (connecting) MaterialTheme.typography.labelMedium else MaterialTheme.typography.labelSmall,
                color = colors.primary, textAlign = TextAlign.Center)
        }
    }
}

@Composable
private fun GearIcon() {
    val colors = MaterialTheme.colorScheme
    Canvas(Modifier.size(22.dp)) {
        val path = Path()
        repeat(32) { index ->
            val angle = Math.toRadians(index * 11.25 - 5.625)
            val radius = size.minDimension * if (index % 4 < 2) .5f else .37f
            val x = center.x + cos(angle).toFloat() * radius
            val y = center.y + sin(angle).toFloat() * radius
            if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
        }
        path.close()
        drawPath(path, colors.onSurface)
        drawCircle(colors.surface, size.minDimension * .18f)
    }
}

@Composable
private fun DashboardCard(content: @Composable ColumnScope.() -> Unit) {
    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(14.dp), color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)) {
        Column(Modifier.padding(16.dp), horizontalAlignment = Alignment.CenterHorizontally, content = content)
    }
}

@Composable
private fun CardHeading(text: String) {
    val strings = LocalStrings.current
    Text(strings[text], Modifier.fillMaxWidth(), style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
}

@Composable
private fun ReadinessStatus(label: String, value: String, ready: Boolean, failed: Boolean = false, waiting: Boolean = false) {
    val strings = LocalStrings.current
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(strings[label], Modifier.weight(1f), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(strings[value], style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold,
            color = when {
                ready -> MaterialTheme.colorScheme.secondary
                failed -> MaterialTheme.colorScheme.error
                waiting -> MaterialTheme.colorScheme.tertiary
                else -> MaterialTheme.colorScheme.onSurfaceVariant
            })
    }
}

@Composable
private fun ThemeToggle(theme: AppTheme, onChange: (AppTheme) -> Unit, modifier: Modifier = Modifier) {
    val strings = LocalStrings.current
    val dark = when (theme) { AppTheme.Dark -> true; AppTheme.Light -> false; else -> isSystemInDarkTheme() }
    val colors = MaterialTheme.colorScheme
    IconButton(onClick = { onChange(if (dark) AppTheme.Light else AppTheme.Dark) },
        modifier = modifier.semantics {
            stateDescription = strings[if (dark) "Dark Theme" else "Light Theme"]
            contentDescription = strings[if (dark) "Light Theme" else "Dark Theme"]
        },
        colors = IconButtonDefaults.iconButtonColors(contentColor = colors.primary)) {
        Canvas(Modifier.size(22.dp)) {
            if (dark) {
                val moon = Path().apply {
                    moveTo(size.width * .66f, size.height * .06f)
                    cubicTo(size.width * .04f, -size.height * .02f, -size.width * .04f, size.height * .95f, size.width * .62f, size.height * .97f)
                    cubicTo(size.width * .83f, size.height * .97f, size.width * .98f, size.height * .83f, size.width, size.height * .68f)
                    cubicTo(size.width * .44f, size.height * .77f, size.width * .36f, size.height * .25f, size.width * .66f, size.height * .06f)
                    close()
                }
                drawPath(moon, colors.primary)
            } else {
                drawCircle(colors.primary, size.minDimension * .22f, style = Stroke(2.dp.toPx()))
                repeat(8) { index ->
                    val angle = index * Math.PI / 4
                    val unit = Offset(cos(angle).toFloat(), sin(angle).toFloat())
                    drawLine(colors.primary, center + unit * size.minDimension * .34f,
                        center + unit * size.minDimension * .46f, 2.dp.toPx(), StrokeCap.Round)
                }
            }
        }
    }
}
