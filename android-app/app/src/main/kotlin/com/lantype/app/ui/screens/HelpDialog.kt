package com.lantype.app.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import com.lantype.app.ui.theme.BluePrimary
import com.lantype.app.ui.theme.CyanAccent
import com.lantype.app.ui.theme.Slate400
import com.lantype.app.ui.theme.Slate800

@Composable
fun HelpDialog(
    onDismiss: () -> Unit
) {
    Dialog(onDismissRequest = onDismiss) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = Slate800)
        ) {
            Column(
                modifier = Modifier
                    .padding(20.dp)
                    .verticalScroll(rememberScrollState())
            ) {
                // Header
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "How to Use LANType",
                        color = Color.White,
                        fontSize = 17.sp,
                        fontWeight = FontWeight.Bold
                    )
                    IconButton(onClick = onDismiss, modifier = Modifier.size(24.dp)) {
                        Icon(imageVector = Icons.Default.Close, contentDescription = "Close", tint = Slate400)
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                HelpSection(
                    title = "1. Connect to Windows Hotspot",
                    content = "Enable Mobile Hotspot in Windows Settings. Connect your Android phone to the laptop hotspot Wi-Fi network."
                )

                HelpSection(
                    title = "2. Pair Your Phone",
                    content = "Open LANType on your laptop. Click 'Pair Laptop' in this app, then scan the QR code displayed on the laptop or enter the 6-digit PIN manually."
                )

                HelpSection(
                    title = "3. Enter Text & Set Speed",
                    content = "Paste or enter the text you want to type. Set your desired speed in CPM (characters per minute) or WPM (words per minute)."
                )

                HelpSection(
                    title = "4. Focus Target App & Start",
                    content = "Click inside Notepad, Word, a browser text field, or a kiosk window on your laptop. Then tap 'Start Typing'. The text streams directly through the Windows Virtual HID Keyboard driver."
                )

                HelpSection(
                    title = "5. Safety Controls",
                    content = "You can Pause, Resume, or Stop at any time. The Emergency Killswitch immediately halts typing and releases all keyboard modifiers."
                )

                Spacer(modifier = Modifier.height(16.dp))

                Button(
                    onClick = onDismiss,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(10.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = BluePrimary)
                ) {
                    Text("Got it", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun HelpSection(title: String, content: String) {
    Column(modifier = Modifier.padding(bottom = 12.dp)) {
        Text(text = title, color = CyanAccent, fontSize = 13.sp, fontWeight = FontWeight.Bold)
        Spacer(modifier = Modifier.height(2.dp))
        Text(text = content, color = Slate400, fontSize = 12.sp, lineHeight = 16.sp)
    }
}
