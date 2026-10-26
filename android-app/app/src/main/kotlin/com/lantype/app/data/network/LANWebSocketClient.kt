package com.lantype.app.data.network

import android.util.Log
import com.lantype.app.data.model.HeartbeatMessage
import com.lantype.app.data.model.ProtocolJson
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.util.concurrent.TimeUnit

enum class ConnectionStatus {
    Disconnected,
    Connecting,
    Connected,
    Error
}

class LANWebSocketClient {
    private val tag = "LANWebSocketClient"

    private val client = OkHttpClient.Builder()
        .pingInterval(10, TimeUnit.SECONDS)
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.MILLISECONDS) // Indefinite read for WebSocket
        .build()

    private var webSocket: WebSocket? = null
    private var heartbeatJob: Job? = null
    private val scope = CoroutineScope(Dispatchers.IO + Job())

    private val _connectionStatus = MutableStateFlow(ConnectionStatus.Disconnected)
    val connectionStatus: StateFlow<ConnectionStatus> = _connectionStatus.asStateFlow()

    private val _incomingMessages = MutableSharedFlow<String>(extraBufferCapacity = 64)
    val incomingMessages: SharedFlow<String> = _incomingMessages.asSharedFlow()

    private var currentHost: String? = null
    private var currentPort: Int = 52443

    fun connect(host: String, port: Int) {
        disconnect()

        currentHost = host
        currentPort = port
        _connectionStatus.value = ConnectionStatus.Connecting

        val url = "ws://$host:$port/ws"
        Log.d(tag, "Connecting to WebSocket at $url")

        val request = Request.Builder()
            .url(url)
            .build()

        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                Log.i(tag, "WebSocket connection opened to $url")
                _connectionStatus.value = ConnectionStatus.Connected
                startHeartbeat()
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                scope.launch {
                    _incomingMessages.emit(text)
                }
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                Log.d(tag, "WebSocket closing: $code / $reason")
                webSocket.close(1000, null)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                Log.i(tag, "WebSocket closed: $code / $reason")
                stopHeartbeat()
                _connectionStatus.value = ConnectionStatus.Disconnected
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                Log.e(tag, "WebSocket failure: ${t.message}", t)
                stopHeartbeat()
                _connectionStatus.value = ConnectionStatus.Error
            }
        })
    }

    fun sendMessage(json: String): Boolean {
        val ws = webSocket
        if (ws != null && _connectionStatus.value == ConnectionStatus.Connected) {
            return ws.send(json)
        }
        Log.w(tag, "Cannot send message: WebSocket is not connected.")
        return false
    }

    private fun startHeartbeat() {
        stopHeartbeat()
        heartbeatJob = scope.launch {
            var seq = 0L
            while (isActive && _connectionStatus.value == ConnectionStatus.Connected) {
                delay(15000)
                val ping = HeartbeatMessage(sequence = seq++)
                val json = ProtocolJson.json.encodeToString(HeartbeatMessage.serializer(), ping)
                sendMessage(json)
            }
        }
    }

    private fun stopHeartbeat() {
        heartbeatJob?.cancel()
        heartbeatJob = null
    }

    fun disconnect() {
        stopHeartbeat()
        try {
            webSocket?.close(1000, "User disconnected")
        } catch (_: Exception) {
        }
        webSocket = null
        _connectionStatus.value = ConnectionStatus.Disconnected
    }
}
