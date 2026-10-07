package com.example.ezacrosscontrol.protocol

import org.json.JSONObject

object InputEventParser {
    fun parse(jsonString: String): MessageEnvelope? {
        return try {
            val json = JSONObject(jsonString)
            val type = json.optString("type", json.optString("Type", ""))
            val protocolVersion = json.optInt("protocolVersion", json.optInt("ProtocolVersion", 0))
            val sequence = json.optLong("sequence", json.optLong("Sequence", 0L))
            val timestamp = json.optLong("timestamp", json.optLong("Timestamp", 0L))
            val payload = json.optJSONObject("payload") ?: json.optJSONObject("Payload") ?: JSONObject()

            MessageEnvelope(type, protocolVersion, sequence, timestamp, payload)
        } catch (e: Exception) {
            null
        }
    }
}
