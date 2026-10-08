package com.example.ezacrosscontrol

import android.os.Build
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
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.Role
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
    isConnected: Boolean, isConnecting: Boolean, connectionProgress: Float,
    controlTransport: String?, status: String, latency: Long?,
    controlState: ControlState, accessibilityEnabled: Boolean, controlStopped: Boolean,
    usbDebugEnabled: Boolean?, wifiDebugEnabled: Boolean?,
    lastEvent: String, lastSequence: Long, currentHz: Int, logText: String,
    onIpChange: (String) -> Unit, onPortChange: (String) -> Unit,
    onThemeChange: (AppTheme) -> Unit, onConnect: () -> Unit, onDisconnect: () -> Unit,
    onOpenAccessibility: () -> Unit, onOpenDeveloperSettings: () -> Unit,
    onStopControl: () -> Unit, onEnableIme: () -> Unit, onSelectIme: () -> Unit,
) {
    var advancedOpen by remember { mutableStateOf(false) }
    var diagnosticsOpen by remember { mutableStateOf(false) }
    var themeMenuOpen by remember { mutableStateOf(false) }
    val colors = MaterialTheme.colorScheme
    val corner = RoundedCornerShape(10.dp)

    // Leave the real Android status bar, cutouts and navigation area clear.
    Box(Modifier.fillMaxSize().safeDrawingPadding(), contentAlignment = Alignment.TopCenter) {
        Column(
            Modifier.widthIn(max = 480.dp).fillMaxWidth()
                .verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Row(Modifier.fillMaxWidth().padding(vertical = 8.dp),
                horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                Image(painterResource(R.drawable.ez_across_logo), "Logo EZ Across Control",
                    Modifier.size(44.dp).clip(RoundedCornerShape(10.dp)))
                Spacer(Modifier.width(10.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("EZ", Modifier.alignByBaseline(), fontFamily = FrauncesBrand,
                        fontWeight = FontWeight.ExtraBold, fontSize = 28.sp, maxLines = 1)
                    Text(" Across Control", Modifier.alignByBaseline(), fontFamily = FrauncesBrand,
                        fontWeight = FontWeight.Medium, fontStyle = FontStyle.Italic,
                        fontSize = 26.sp, maxLines = 1)
                }
            }
            DashboardCard {
                Text("CONTROLE ENTRE TELAS", Modifier.fillMaxWidth(), textAlign = TextAlign.Center,
                    style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                Spacer(Modifier.height(20.dp))
                ConnectionCircle(isConnected, isConnecting, connectionProgress,
                    onToggle = { if (isConnected || isConnecting) onDisconnect() else onConnect() })
                Spacer(Modifier.height(18.dp))
                Text(when {
                    isConnected -> controlTransport.orEmpty()
                    isConnecting -> "Aguardando Host"
                    status.isNotBlank() -> status
                    else -> "Pronto para conectar"
                }, Modifier.fillMaxWidth(), textAlign = TextAlign.Center,
                    style = MaterialTheme.typography.bodySmall,
                    color = if (isConnected) colors.secondary else colors.onSurfaceVariant,
                    fontWeight = if (isConnected) FontWeight.SemiBold else FontWeight.Normal)
            }
            DashboardCard {
                CardHeading("Host PC")
                Spacer(Modifier.height(8.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedTextField(ip, onIpChange, Modifier.weight(1f),
                        enabled = !isConnected && !isConnecting,
                        label = { Text("Endereço do IP") }, singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri), shape = corner)
                    OutlinedTextField(port, onPortChange, Modifier.width(92.dp),
                        enabled = !isConnected && !isConnecting,
                        label = { Text("Porta") }, singleLine = true,
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
                    Text("Modo Avançado", Modifier.weight(1f),
                        style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                    Text(if (advancedOpen) "⌃" else "⌄", color = colors.onSurfaceVariant)
                }
                Column(Modifier.animateContentSize()) {
                    if (advancedOpen) {
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        CardHeading("Remote Control")
                        Spacer(Modifier.height(12.dp))
                        ReadinessStatus("Accessibility:", if (accessibilityEnabled) "Enable" else "Disabled", accessibilityEnabled)
                        Spacer(Modifier.height(8.dp))
                        val ready = accessibilityEnabled && !controlStopped && controlState != ControlState.Disabled
                        ReadinessStatus("Remote Control:",
                            if (controlStopped) "Stopped" else if (ready) "Ready" else "Waiting", ready)
                        Spacer(Modifier.height(12.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onOpenAccessibility, Modifier.weight(1f), shape = corner) { Text("Open Settings") }
                            Button(onStopControl, Modifier.weight(1f), enabled = isConnected && !controlStopped,
                                shape = corner) { Text("Stop Control") }
                        }
                        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU && !EZAcrossInputMethodService.isReady()) {
                            Spacer(Modifier.height(8.dp))
                            Text("Remote Keyboard", style = MaterialTheme.typography.labelMedium, color = colors.onSurfaceVariant)
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                TextButton(onEnableIme) { Text("Enable Keyboard") }
                                TextButton(onSelectIme) { Text("Select Keyboard") }
                            }
                        }
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        CardHeading("Depuração")
                        Spacer(Modifier.height(12.dp))
                        ReadinessStatus("USB:", debugStatus(usbDebugEnabled), usbDebugEnabled == true)
                        Spacer(Modifier.height(8.dp))
                        ReadinessStatus("Wi-Fi:", debugStatus(wifiDebugEnabled), wifiDebugEnabled == true)
                        Spacer(Modifier.height(12.dp))
                        OutlinedButton(onOpenDeveloperSettings, Modifier.fillMaxWidth(), shape = corner) { Text("Open Settings") }
                        Text("Opções do Desenvolvedor", Modifier.fillMaxWidth(), textAlign = TextAlign.Center,
                            style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                        HorizontalDivider(Modifier.padding(vertical = 14.dp), color = colors.outline)
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically) {
                            Text("Tema", style = MaterialTheme.typography.bodySmall, color = colors.onSurfaceVariant)
                            Box {
                                TextButton({ themeMenuOpen = true }) { Text(theme.name) }
                                DropdownMenu(themeMenuOpen, { themeMenuOpen = false }) {
                                    AppTheme.entries.forEach { option ->
                                        DropdownMenuItem({ Text(option.name) }, {
                                            onThemeChange(option); themeMenuOpen = false
                                        })
                                    }
                                }
                            }
                        }
                        TextButton({ diagnosticsOpen = !diagnosticsOpen }, Modifier.fillMaxWidth()) { Text("Diagnostics & Activity Log") }
                        if (diagnosticsOpen) {
                            ReadinessStatus("Latency:", latency?.let { "$it ms" } ?: "—", false)
                            ReadinessStatus("Last Event:", lastEvent, false)
                            ReadinessStatus("Sequence:", lastSequence.toString(), false)
                            ReadinessStatus("Rate:", "$currentHz ev/s", false)
                            Spacer(Modifier.height(8.dp))
                            Text(logText.ifBlank { "No activity yet." },
                                style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                        }
                    }
                }
            }
        }
    }
}

private fun debugStatus(enabled: Boolean?): String = when (enabled) {
    true -> "Ready"; false -> "Disabled"; null -> "Unavailable"
}

@Composable
private fun ConnectionCircle(connected: Boolean, connecting: Boolean, progress: Float, onToggle: () -> Unit) {
    val colors = MaterialTheme.colorScheme
    val animatedProgress by animateFloatAsState(
        if (connected) 1f else if (connecting) progress.coerceIn(0f, .95f) else 0f,
        tween(450), label = "Connection Progress")
    Box(Modifier.size(224.dp).clip(CircleShape).semantics {
        stateDescription = when { connected -> "Conectado"; connecting -> "Aguardando Host"; else -> "Desconectado" }
        if (connecting) progressBarRangeInfo = ProgressBarRangeInfo(animatedProgress, 0f..1f)
    }.clickable(role = Role.Button, onClickLabel = if (connected || connecting) "Desconectar" else "Conectar",
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
            Text(when { connected -> "Conectado"; connecting -> "Aguardando"; else -> "Conectar" },
                fontSize = 22.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center)
            Text(when { connected -> "TOQUE PARA SAIR"; connecting -> "${(animatedProgress * 100).roundToInt()}%"; else -> "TOQUE AQUI" },
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
    Text(text, Modifier.fillMaxWidth(), style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
}

@Composable
private fun ReadinessStatus(label: String, value: String, ready: Boolean) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, Modifier.weight(1f), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold,
            color = if (ready) MaterialTheme.colorScheme.secondary else MaterialTheme.colorScheme.onSurfaceVariant)
    }
}
