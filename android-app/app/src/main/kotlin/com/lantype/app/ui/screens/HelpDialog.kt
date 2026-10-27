package com.lantype.app.ui.screens

import androidx.compose.foundation.BorderStroke
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import com.lantype.app.ui.theme.AccentWhite
import com.lantype.app.ui.theme.BlackBg
import com.lantype.app.ui.theme.BorderColor
import com.lantype.app.ui.theme.SurfaceDark
import com.lantype.app.ui.theme.TextPrimary
import com.lantype.app.ui.theme.TextSecondary

@Composable
fun HelpDialog(
    onDismiss: () -> Unit
) {
    Dialog(onDismissRequest = onDismiss) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(8.dp),
            border = BorderStroke(1.dp, BorderColor),
            colors = CardDefaults.cardColors(containerColor = SurfaceDark)
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
                        text = "User Guide",
                        color = TextPrimary,
                        fontSize = 15.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                    IconButton(onClick = onDismiss, modifier = Modifier.size(24.dp)) {
                        Icon(imageVector = Icons.Default.Close, contentDescription = "Close", tint = TextSecondary)
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                HelpSection(
                    title = "1. Connect to Windows Hotspot",
                    content = "Turn on Mobile Hotspot in Windows Settings. Connect your Android phone to that Wi-Fi hotspot."
                )

                HelpSection(
                    title = "2. Pair Your Phone",
                    content = "Open LANType on your laptop. Tap 'Pair Laptop', then scan the QR code displayed on screen or enter the 6-digit PIN manually."
                )

                HelpSection(
                    title = "3. Enter Text & Configure Speed",
                    content = "Paste or type the text you want to transfer. Adjust your desired typing rate in CPM (characters per minute) or WPM."
                )

                HelpSection(
                    title = "4. Focus Target App & Start",
                    content = "Focus Notepad, Word, a browser input, or terminal on your laptop, then tap 'Start Typing into Laptop'. Text streams directly through the Virtual HID Keyboard driver."
                )

                HelpSection(
                    title = "5. Safety Controls",
                    content = "You can Pause, Resume, or Stop at any time. The Emergency Killswitch immediately halts typing and clears all modifier keystrokes."
                )

                Spacer(modifier = Modifier.height(16.dp))

                Button(
                    onClick = onDismiss,
                    modifier = Modifier.fillMaxWidth().height(44.dp),
                    shape = RoundedCornerShape(6.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = AccentWhite,
                        contentColor = BlackBg
                    )
                ) {
                    Text("Got it", fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                }
            }
        }
    }
}

@Composable
private fun HelpSection(title: String, content: String) {
    Column(modifier = Modifier.padding(bottom = 12.dp)) {
        Text(text = title, color = TextPrimary, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        Spacer(modifier = Modifier.height(2.dp))
        Text(text = content, color = TextSecondary, fontSize = 12.sp, lineHeight = 16.sp)
    }
}
