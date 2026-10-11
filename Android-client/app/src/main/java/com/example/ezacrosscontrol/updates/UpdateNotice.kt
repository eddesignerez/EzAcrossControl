package com.example.ezacrosscontrol.updates

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.ui.unit.dp
import androidx.compose.material3.*
import androidx.compose.runtime.*
import com.example.ezacrosscontrol.AppStrings

@Composable
internal fun UpdateNotice(updater: AndroidUpdater, strings: AppStrings) {
    val state by updater.state.collectAsState()
    if (state.phase == "idle") return
    AlertDialog(
        onDismissRequest = { updater.cancel() },
        title = { Text(strings["Update Available"]) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(strings["Update Android Offer"].replace("{0}", state.version))
                if (state.message.isNotEmpty()) Text(strings[state.message], color = MaterialTheme.colorScheme.error)
                if (state.phase == "download" || state.phase == "validate") {
                    Text(if (state.phase == "validate") strings["Update Validating"]
                        else strings["Update Downloading"].replace("{0}", state.progress.toString()))
                    LinearProgressIndicator(progress = { state.progress / 100f })
                }
                if (state.phase == "permission") Text(strings["Update Permission Needed"])
            }
        },
        confirmButton = {
            if (state.phase == "offer") TextButton(onClick = { updater.accept() }) { Text(strings["Update Now"]) }
        },
        dismissButton = { TextButton(onClick = { updater.cancel() }) {
            Text(strings[if (state.phase == "offer") "Update Later" else "Update Cancel"])
        } }
    )
}
