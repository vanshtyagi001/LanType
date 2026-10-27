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
    Reconnecting,
    Error
}

class LANWebSocketClient {
    private val tag = "LANWebSocketClient"

    private val client = OkHttpClient.Builder()
        .pingInterval(25, TimeUnit.SECONDS)
        .connectTimeout(6, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.MILLISECONDS) // Indefinite read for WebSocket
        .retryOnConnectionFailure(true)
        .build()

    private var webSocket: WebSocket? = null
    private var heartbeatJob: Job? = null
    private var reconnectJob: Job? = null
    private val scope = CoroutineScope(Dispatchers.IO + Job())

    private val _connectionStatus = MutableStateFlow(ConnectionStatus.Disconnected)
    val connectionStatus: StateFlow<ConnectionStatus> = _connectionStatus.asStateFlow()

    private val _incomingMessages = MutableSharedFlow<String>(extraBufferCapacity = 64)
    val incomingMessages: SharedFlow<String> = _incomingMessages.asSharedFlow()

    private var currentHost: String? = null
    private var currentPort: Int = 52443
    private var isUserDisconnected = false
    private var reconnectAttempts = 0
    private val maxReconnectAttempts = 15

    fun connect(host: String, port: Int) {
        isUserDisconnected = false
        reconnectAttempts = 0
        reconnectJob?.cancel()
        currentHost = host
        currentPort = port

        _connectionStatus.value = ConnectionStatus.Connecting
        executeConnect(host, port)
    }

    private fun executeConnect(host: String, port: Int) {
        stopHeartbeat()
        try {
            webSocket?.close(1000, "Resetting connection")
        } catch (_: Exception) {}

        val url = "ws://$host:$port/ws"
        Log.d(tag, "Connecting to WebSocket at $url")

        val request = Request.Builder()
            .url(url)
            .build()

        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                Log.i(tag, "WebSocket connection opened to $url")
                reconnectAttempts = 0
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

                if (!isUserDisconnected && code != 1000) {
                    scheduleReconnect()
                } else {
                    _connectionStatus.value = ConnectionStatus.Disconnected
                }
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                Log.e(tag, "WebSocket failure: ${t.message}", t)
                stopHeartbeat()

                if (!isUserDisconnected) {
                    scheduleReconnect()
                } else {
                    _connectionStatus.value = ConnectionStatus.Error
                }
            }
        })
    }

    private fun scheduleReconnect() {
        if (isUserDisconnected) return

        val host = currentHost ?: return
        val port = currentPort

        if (reconnectAttempts >= maxReconnectAttempts) {
            Log.w(tag, "Max reconnect attempts reached ($maxReconnectAttempts). Setting state to Error.")
            _connectionStatus.value = ConnectionStatus.Error
            return
        }

        reconnectAttempts++
        _connectionStatus.value = ConnectionStatus.Reconnecting

        reconnectJob?.cancel()
        reconnectJob = scope.launch {
            val backoffMs = (1000L * reconnectAttempts).coerceAtMost(5000L)
            Log.i(tag, "Attempting auto-reconnect #$reconnectAttempts in ${backoffMs}ms...")
            delay(backoffMs)

            if (!isUserDisconnected && isActive) {
                executeConnect(host, port)
            }
        }
    }

    fun reconnect() {
        val host = currentHost ?: return
        reconnectAttempts = 0
        isUserDisconnected = false
        _connectionStatus.value = ConnectionStatus.Reconnecting
        executeConnect(host, currentPort)
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
                delay(12000)
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
        isUserDisconnected = true
        reconnectJob?.cancel()
        reconnectJob = null
        stopHeartbeat()
        try {
            webSocket?.close(1000, "User disconnected")
        } catch (_: Exception) {}
        webSocket = null
        _connectionStatus.value = ConnectionStatus.Disconnected
    }
}
