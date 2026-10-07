package com.example.ezacrosscontrol

import android.os.Build
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.ezacrosscontrol.control.ControlState
import com.example.ezacrosscontrol.control.keyboard.EZAcrossInputMethodService
import com.example.ezacrosscontrol.data.AppTheme
import com.example.ezacrosscontrol.theme.FrauncesBrand

@Composable
internal fun DashboardScreen(
    ip: String,
    port: String,
    theme: AppTheme,
    isConnected: Boolean,
    status: String,
    latency: Long?,
    controlState: ControlState,
    lastEvent: String,
    lastSequence: Long,
    currentHz: Int,
    logText: String,
    onIpChange: (String) -> Unit,
    onPortChange: (String) -> Unit,
    onThemeChange: (AppTheme) -> Unit,
    onConnect: () -> Unit,
    onDisconnect: () -> Unit,
    onOpenAccessibility: () -> Unit,
    onStopControl: () -> Unit,
    onEnableIme: () -> Unit,
    onSelectIme: () -> Unit,
) {
    var themeMenuOpen by remember { mutableStateOf(false) }
    var diagnosticsOpen by remember { mutableStateOf(false) }
    val colors = MaterialTheme.colorScheme
    val corner = RoundedCornerShape(10.dp)

    // Edge-to-edge stays enabled in MainActivity. This inset keeps the real Android
    // clock, connection icons, cutouts, and navigation area outside app content.
    Column(
        modifier = Modifier
            .fillMaxSize()
            .safeDrawingPadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Image(
                    painter = painterResource(R.drawable.ez_across_logo),
                    contentDescription = "EZ Across Control logo",
                    modifier = Modifier.size(42.dp).clip(RoundedCornerShape(10.dp)),
                )
                Column {
                    Row(verticalAlignment = Alignment.Bottom) {
                        Text(
                            "EZ",
                            fontFamily = FrauncesBrand,
                            fontWeight = FontWeight.ExtraBold,
                            fontSize = 20.sp,
                            lineHeight = 23.sp,
                            maxLines = 1,
                        )
                        Text(
                            " Across Control",
                            fontFamily = FrauncesBrand,
                            fontWeight = FontWeight.Medium,
                            fontStyle = FontStyle.Italic,
                            fontSize = 17.sp,
                            lineHeight = 22.sp,
                            maxLines = 1,
                        )
                    }
                    Text("Connect to your Windows PC", style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                }
            }
            Box {
                Surface(
                    shape = RoundedCornerShape(8.dp),
                    color = colors.surface,
                    border = BorderStroke(1.dp, colors.outline),
                ) {
                    TextButton(onClick = { themeMenuOpen = true }) {
                        Text(theme.name, style = MaterialTheme.typography.labelSmall)
                    }
                }
                DropdownMenu(expanded = themeMenuOpen, onDismissRequest = { themeMenuOpen = false }) {
                    AppTheme.entries.forEach { option ->
                        DropdownMenuItem(
                            text = { Text(option.name) },
                            onClick = {
                                onThemeChange(option)
                                themeMenuOpen = false
                            },
                        )
                    }
                }
            }
        }

        DashboardCard {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(7.dp)) {
                Box(
                    Modifier.size(7.dp).background(
                        if (isConnected) colors.secondary else colors.onSurfaceVariant,
                        CircleShape,
                    ),
                )
                Text("CONNECTION STATUS", style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
            }
            Spacer(Modifier.height(5.dp))
            Text(status, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
            Text(
                if (isConnected) "Connected to your Windows PC on the local network."
                else "Connect on your local network to use your keyboard and mouse across devices.",
                style = MaterialTheme.typography.bodySmall,
                color = colors.onSurfaceVariant,
            )
        }

        DashboardCard {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                CardHeading("Windows connection")
                Text("LAN", style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
            }
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = ip,
                    onValueChange = onIpChange,
                    label = { Text("PC IP address") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                    shape = corner,
                    modifier = Modifier.weight(1f),
                )
                OutlinedTextField(
                    value = port,
                    onValueChange = onPortChange,
                    label = { Text("Port") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    shape = corner,
                    modifier = Modifier.weight(0.48f),
                )
            }
            Spacer(Modifier.height(11.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(
                    onClick = onConnect,
                    enabled = !isConnected,
                    shape = corner,
                    modifier = Modifier.weight(1f),
                ) { Text("Connect to PC", maxLines = 1) }
                OutlinedButton(
                    onClick = onDisconnect,
                    enabled = isConnected,
                    shape = corner,
                    modifier = Modifier.weight(0.78f),
                ) { Text("Disconnect", maxLines = 1) }
            }
        }

        DashboardCard {
            CardHeading("Control readiness")
            Spacer(Modifier.height(12.dp))
            ReadinessRow(
                number = "1",
                title = "Accessibility",
                detail = if (controlState != ControlState.Disabled) "Enabled for remote pointer and gestures" else "Enable remote pointer and gestures",
                action = "Open",
                onAction = onOpenAccessibility,
            )
            HorizontalDivider(Modifier.padding(vertical = 11.dp), color = colors.outline)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                ReadinessRow(
                    number = "2",
                    title = "Remote keyboard",
                    detail = "Automatic input ownership on this Android version",
                )
            } else {
                ReadinessRow(
                    number = "2",
                    title = "Remote keyboard",
                    detail = if (EZAcrossInputMethodService.isReady()) "Enabled and selected" else "Enable and select the input method",
                    action = "Enable",
                    onAction = onEnableIme,
                )
                if (!EZAcrossInputMethodService.isReady()) {
                    TextButton(onClick = onSelectIme, modifier = Modifier.align(Alignment.End)) { Text("Select keyboard") }
                }
            }
        }

        DashboardCard {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                CardHeading("Session")
                Text(
                    latency?.let { "$it ms latency" } ?: "— ms latency",
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(9.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text("Remote control", style = MaterialTheme.typography.bodySmall, color = colors.onSurfaceVariant)
                Text(controlState.name, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
            }
            if (controlState == ControlState.Controlling || controlState == ControlState.Ready) {
                Spacer(Modifier.height(9.dp))
                OutlinedButton(onClick = onStopControl, shape = corner) { Text("Stop control") }
            }
        }

        TextButton(onClick = { diagnosticsOpen = !diagnosticsOpen }, modifier = Modifier.fillMaxWidth()) {
            Text(if (diagnosticsOpen) "Hide session diagnostics" else "Show session diagnostics")
        }
        if (diagnosticsOpen) {
            DashboardCard {
                CardHeading("Input session")
                Spacer(Modifier.height(8.dp))
                DiagnosticRow("Last event", lastEvent)
                DiagnosticRow("Sequence", lastSequence.toString())
                DiagnosticRow("Rate", "$currentHz ev/s")
                HorizontalDivider(Modifier.padding(vertical = 10.dp), color = colors.outline)
                CardHeading("Activity log")
                Spacer(Modifier.height(7.dp))
                Text(
                    logText.ifBlank { "No activity yet." },
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun DashboardCard(content: @Composable ColumnScope.() -> Unit) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
    ) {
        Column(Modifier.padding(16.dp), content = content)
    }
}

@Composable
private fun CardHeading(text: String) {
    Text(text, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
}

@Composable
private fun ReadinessRow(
    number: String,
    title: String,
    detail: String,
    action: String? = null,
    onAction: (() -> Unit)? = null,
) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top, horizontalArrangement = Arrangement.spacedBy(9.dp)) {
        Surface(shape = RoundedCornerShape(6.dp), color = MaterialTheme.colorScheme.surfaceVariant) {
            Text(number, Modifier.padding(horizontal = 7.dp, vertical = 3.dp), style = MaterialTheme.typography.labelSmall)
        }
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
            Text(detail, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        if (action != null && onAction != null) {
            TextButton(onClick = onAction) { Text(action, maxLines = 1) }
        }
    }
}

@Composable
private fun DiagnosticRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, modifier = Modifier.weight(1f), style = MaterialTheme.typography.bodySmall,
            textAlign = TextAlign.End, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}
