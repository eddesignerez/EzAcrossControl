package com.example.ezacrosscontrol

import android.os.Looper
import okhttp3.*
import okio.ByteString
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.annotation.LooperMode
import java.io.IOException
import java.time.Duration

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [30])
@LooperMode(LooperMode.Mode.PAUSED)
class WebSocketClientTest {
    private class FakeSocket(private val request: Request) : WebSocket {
        var closeCode: Int? = null
        override fun request() = request
        override fun queueSize() = 0L
        override fun send(text: String) = true
        override fun send(bytes: ByteString) = true
        override fun close(code: Int, reason: String?): Boolean { closeCode = code; return true }
        override fun cancel() = Unit
    }

    private class Harness {
        val sockets = mutableListOf<FakeSocket>()
        val listeners = mutableListOf<WebSocketListener>()
        val failures = mutableListOf<Pair<String, Boolean>>()
        val states = mutableListOf<Boolean>()
        val client = WebSocketClient(RuntimeEnvironment.getApplication()) { _, request, listener ->
            val socket = FakeSocket(request)
            sockets += socket
            listeners += listener
            // Exercise the existing fast-LAN callback race without opening a network socket.
            listener.onOpen(socket, Response.Builder().request(request).protocol(Protocol.HTTP_1_1)
                .code(101).message("Switching Protocols").build())
            socket
        }.apply {
            onConnectionFailure = { message, retrying -> failures += message to retrying }
            onConnectionStateChanged = { states += it }
        }
        fun connect(mode: String = "USB") = client.connect("192.0.2.1", "8765", mode)
    }

    @Test fun manualConflictStopsRetryingAndAllowsANewChoice() {
        val h = Harness()
        h.connect()
        val oldSocket = h.sockets.single()
        val oldListener = h.listeners.single()
        val reason = "Connection modes conflict; choose the same mode or Auto"
        oldListener.onClosing(oldSocket, 1008, reason)
        assertEquals(1008, oldSocket.closeCode)
        assertEquals(reason to false, h.failures.single())
        assertEquals(listOf(false), h.states)
        shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(31))
        assertEquals(1, h.sockets.size)

        h.connect("Auto")
        val currentSocket = h.sockets.last()
        h.listeners.last().onMessage(currentSocket, """{"Type":"WELCOME","ProtocolVersion":2,"Payload":{}}""")
        oldListener.onClosed(oldSocket, 1008, reason)
        assertTrue(h.states.last())
        assertEquals(2, h.sockets.size)
        h.client.disconnect()
    }

    @Test fun policyClosureWithoutOnClosingIsStillTerminal() {
        val h = Harness()
        h.connect()
        h.listeners.single().onClosed(h.sockets.single(), 1008, "Pairing refused")
        assertEquals("Pairing refused" to false, h.failures.single())
        shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(31))
        assertEquals(1, h.sockets.size)
    }

    @Test fun transientFailureReportsRetryAndCreatesAnotherAttempt() {
        val h = Harness()
        h.connect("Auto")
        h.listeners.single().onFailure(h.sockets.single(), IOException("test interruption"), null)
        assertTrue(h.failures.single().second)
        shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(1))
        assertEquals(2, h.sockets.size)
        h.client.disconnect()
    }

    @Test fun invalidAddressReportsTerminalFailureWithoutOpeningSocket() {
        val h = Harness()
        h.client.connect("invalid", "8765")
        assertFalse(h.failures.single().second)
        assertTrue(h.sockets.isEmpty())
    }
}
