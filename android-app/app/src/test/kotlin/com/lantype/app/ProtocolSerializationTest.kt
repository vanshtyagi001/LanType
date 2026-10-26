package com.lantype.app

import com.lantype.app.data.model.PairRequestMessage
import com.lantype.app.data.model.PairResponseMessage
import com.lantype.app.data.model.ProtocolJson
import com.lantype.app.data.model.SessionState
import com.lantype.app.data.model.SessionStateMessage
import com.lantype.app.data.model.SpeedUnit
import com.lantype.app.data.model.StartTypingMessage
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ProtocolSerializationTest {

    @Test
    fun testSerializePairRequest() {
        val req = PairRequestMessage(
            deviceName = "Pixel 8",
            pairingPin = "654321"
        )
        val json = ProtocolJson.json.encodeToString(PairRequestMessage.serializer(), req)

        assertTrue(json.contains("\"type\":\"PairRequest\""))
        assertTrue(json.contains("\"deviceName\":\"Pixel 8\""))
        assertTrue(json.contains("\"pairingPin\":\"654321\""))

        val type = ProtocolJson.parseMessageType(json)
        assertEquals("PairRequest", type)
    }

    @Test
    fun testDeserializePairResponse() {
        val json = """
            {
                "type": "PairResponse",
                "messageId": "msg-123",
                "success": true,
                "sessionToken": "token-abc-987",
                "serverVersion": "1.0"
            }
        """.trimIndent()

        val resp = ProtocolJson.json.decodeFromString<PairResponseMessage>(json)
        assertTrue(resp.success)
        assertEquals("token-abc-987", resp.sessionToken)
        assertEquals("1.0", resp.serverVersion)
    }

    @Test
    fun testSerializeStartTyping() {
        val msg = StartTypingMessage(
            sessionToken = "token-xyz",
            text = "Testing 123",
            speedUnit = SpeedUnit.Wpm,
            speedValue = 75
        )
        val json = ProtocolJson.json.encodeToString(StartTypingMessage.serializer(), msg)

        assertTrue(json.contains("\"type\":\"StartTyping\""))
        assertTrue(json.contains("\"speedUnit\":\"Wpm\""))
        assertTrue(json.contains("\"speedValue\":75"))
        assertTrue(json.contains("\"text\":\"Testing 123\""))
    }

    @Test
    fun testDeserializeSessionState() {
        val json = """
            {
                "type": "SessionState",
                "state": "Typing",
                "totalCharacters": 500,
                "charactersTyped": 150,
                "charactersRemaining": 350,
                "currentCpm": 300,
                "currentWpm": 60,
                "elapsedMilliseconds": 30000,
                "estimatedRemainingMilliseconds": 70000
            }
        """.trimIndent()

        val state = ProtocolJson.json.decodeFromString<SessionStateMessage>(json)
        assertEquals(SessionState.Typing, state.state)
        assertEquals(500, state.totalCharacters)
        assertEquals(150, state.charactersTyped)
        assertEquals(350, state.charactersRemaining)
        assertEquals(300, state.currentCpm)
        assertEquals(60, state.currentWpm)
    }
}
