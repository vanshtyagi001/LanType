package com.lantype.app.data.network

import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import com.lantype.app.data.model.DiscoveredDevice
import com.lantype.app.data.model.ProtocolJson
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.BufferedReader
import java.io.File
import java.io.FileReader
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.util.concurrent.TimeUnit

class DeviceDiscoveryManager(private val context: Context) {
    private val tag = "DeviceDiscovery"
    private val scope = CoroutineScope(Dispatchers.IO + Job())

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(800, TimeUnit.MILLISECONDS)
        .readTimeout(800, TimeUnit.MILLISECONDS)
        .build()

    private val _discoveredDevice = MutableStateFlow<DiscoveredDevice?>(null)
    val discoveredDevice: StateFlow<DiscoveredDevice?> = _discoveredDevice.asStateFlow()

    private val _isScanning = MutableStateFlow(false)
    val isScanning: StateFlow<Boolean> = _isScanning.asStateFlow()

    private var discoveryJob: Job? = null
    private var multicastLock: WifiManager.MulticastLock? = null

    private val defaultPort = 52443
    private val discoveryPort = 52442

    fun startDiscovery() {
        if (_isScanning.value) return
        _isScanning.value = true

        discoveryJob = scope.launch {
            acquireMulticastLock()

            // Run active subnet & gateway probes concurrently with UDP listener
            val probeJob = launch { periodicActiveProbing() }
            val udpJob = launch { udpListenerAndProbing() }

            try {
                // Keep discovery active while scanning
                while (isActive && _isScanning.value) {
                    delay(500)
                }
            } finally {
                probeJob.cancel()
                udpJob.cancel()
                releaseMulticastLock()
                _isScanning.value = false
            }
        }
    }

    fun stopDiscovery() {
        _isScanning.value = false
        discoveryJob?.cancel()
        discoveryJob = null
        releaseMulticastLock()
    }

    private suspend fun periodicActiveProbing() {
        while (scope.isActive && _isScanning.value) {
            val candidateIps = mutableSetOf<String>()

            // 1. Check Wi-Fi DHCP Gateway (crucial when connected to Windows Mobile Hotspot or router)
            getWifiGatewayIp()?.let { candidateIps.add(it) }

            // 2. Known default hotspot gateways
            candidateIps.add("192.168.137.1") // Windows Mobile Hotspot default
            candidateIps.add("192.168.43.1")  // Android Hotspot default

            // 3. Read ARP table (crucial when laptop connects to Phone Hotspot)
            candidateIps.addAll(getArpTableIps())

            // Probe all candidates concurrently
            for (ip in candidateIps) {
                if (!scope.isActive) break
                probeHttpEndpoint(ip, defaultPort)
            }

            delay(2500)
        }
    }

    private fun probeHttpEndpoint(ip: String, port: Int) {
        val url = "http://$ip:$port/health"
        try {
            val request = Request.Builder().url(url).build()
            httpClient.newCall(request).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string() ?: ""
                    var hostName = "Windows PC"
                    try {
                        val json = ProtocolJson.json.parseToJsonElement(body).jsonObject
                        hostName = json["hostName"]?.jsonPrimitive?.content ?: "Windows PC"
                    } catch (_: Exception) {
                        if (body.contains("LANType", ignoreCase = true)) {
                            hostName = "LANType PC"
                        }
                    }

                    val device = DiscoveredDevice(hostName = hostName, ip = ip, port = port)
                    Log.i(tag, "Successfully discovered LANType host via HTTP probe: $device")
                    _discoveredDevice.value = device
                }
            }
        } catch (_: Exception) {
            // Not a LANType host or timed out
        }
    }

    private suspend fun udpListenerAndProbing() {
        var socket: DatagramSocket? = null
        try {
            socket = DatagramSocket(null).apply {
                reuseAddress = true
                broadcast = true
                bind(InetSocketAddress(discoveryPort))
                soTimeout = 2000
            }

            // Send initial UDP probe
            sendUdpProbe(socket)

            val buffer = ByteArray(2048)
            val packet = DatagramPacket(buffer, buffer.size)

            var lastProbeTime = System.currentTimeMillis()

            while (scope.isActive && _isScanning.value) {
                try {
                    // Send probe every 3 seconds
                    if (System.currentTimeMillis() - lastProbeTime > 3000) {
                        sendUdpProbe(socket)
                        lastProbeTime = System.currentTimeMillis()
                    }

                    socket.receive(packet)
                    val raw = String(packet.data, 0, packet.length, Charsets.UTF_8).trim()

                    if (raw.contains("lantype_beacon", ignoreCase = true)) {
                        val senderIp = packet.address.hostAddress ?: ""
                        parseAndSetDevice(raw, senderIp)
                    }
                } catch (_: java.net.SocketTimeoutException) {
                    // Timeout expected for periodic checking
                } catch (e: Exception) {
                    Log.d(tag, "UDP receive warning: ${e.message}")
                    delay(500)
                }
            }
        } catch (e: Exception) {
            Log.w(tag, "Could not open UDP discovery socket on port $discoveryPort: ${e.message}")
        } finally {
            socket?.close()
        }
    }

    private fun sendUdpProbe(socket: DatagramSocket) {
        val probeBytes = "LANTYPE_PROBE".toByteArray(Charsets.UTF_8)
        val targets = listOf("255.255.255.255", "192.168.137.255", "192.168.43.255")
        for (target in targets) {
            try {
                val address = InetAddress.getByName(target)
                val packet = DatagramPacket(probeBytes, probeBytes.size, address, discoveryPort)
                socket.send(packet)
            } catch (_: Exception) {}
        }
    }

    private fun parseAndSetDevice(jsonStr: String, fallbackIp: String) {
        try {
            val json = ProtocolJson.json.parseToJsonElement(jsonStr).jsonObject
            val hostName = json["hostName"]?.jsonPrimitive?.content ?: "Windows PC"
            val ip = json["ip"]?.jsonPrimitive?.content?.takeIf { it.isNotBlank() } ?: fallbackIp
            val port = json["port"]?.jsonPrimitive?.intOrNull ?: defaultPort

            if (ip.isNotBlank()) {
                val device = DiscoveredDevice(hostName = hostName, ip = ip, port = port)
                Log.i(tag, "Discovered device via UDP: $device")
                _discoveredDevice.value = device
            }
        } catch (e: Exception) {
            Log.w(tag, "Failed to parse UDP beacon: ${e.message}")
        }
    }

    private fun getWifiGatewayIp(): String? {
        return try {
            val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            val dhcp = wifiManager?.dhcpInfo ?: return null
            val gateway = dhcp.gateway
            if (gateway != 0) {
                String.format(
                    "%d.%d.%d.%d",
                    gateway and 0xff,
                    gateway shr 8 and 0xff,
                    gateway shr 16 and 0xff,
                    gateway shr 24 and 0xff
                )
            } else null
        } catch (_: Exception) {
            null
        }
    }

    private fun getArpTableIps(): List<String> {
        val ips = mutableListOf<String>()
        try {
            val arp = File("/proc/net/arp")
            if (arp.exists() && arp.canRead()) {
                BufferedReader(FileReader(arp)).use { reader ->
                    reader.lineSequence().drop(1).forEach { line ->
                        val parts = line.split(Regex("\\s+"))
                        if (parts.size >= 4) {
                            val ip = parts[0]
                            val mac = parts[3]
                            if (mac != "00:00:00:00:00:00" && ip.matches(Regex("\\d+\\.\\d+\\.\\d+\\.\\d+"))) {
                                ips.add(ip)
                            }
                        }
                    }
                }
            }
        } catch (_: Exception) {}
        return ips
    }

    private fun acquireMulticastLock() {
        try {
            val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            multicastLock = wifiManager?.createMulticastLock("LANTypeMulticastLock")?.apply {
                setReferenceCounted(true)
                acquire()
            }
        } catch (e: Exception) {
            Log.w(tag, "Failed to acquire multicast lock: ${e.message}")
        }
    }

    private fun releaseMulticastLock() {
        try {
            if (multicastLock?.isHeld == true) {
                multicastLock?.release()
            }
            multicastLock = null
        } catch (_: Exception) {}
    }
}
