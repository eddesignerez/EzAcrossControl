package com.example.ezacrosscontrol.protocol

import org.json.JSONObject

data class MessageEnvelope(
    val type: String,
    val protocolVersion: Int,
    val sequence: Long,
    val timestamp: Long,
    val payload: JSONObject
)
