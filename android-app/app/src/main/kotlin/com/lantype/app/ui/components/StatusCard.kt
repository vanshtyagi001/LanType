package com.lantype.app.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Cable
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.data.model.DriverState
import com.lantype.app.data.model.SessionState
import com.lantype.app.data.network.ConnectionStatus
import com.lantype.app.ui.theme.AmberWarning
import com.lantype.app.ui.theme.BluePrimary
import com.lantype.app.ui.theme.CyanAccent
import com.lantype.app.ui.theme.EmeraldGreen
import com.lantype.app.ui.theme.RedDanger
import com.lantype.app.ui.theme.Slate400
import com.lantype.app.ui.theme.Slate700
import com.lantype.app.ui.theme.Slate800
import com.lantype.app.ui.theme.Slate900
import com.lantype.app.ui.viewmodel.MainUiState

@Composable
fun StatusCard(
    uiState: MainUiState,
    onPairClicked: () -> Unit,
    onDisconnectClicked: () -> Unit,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Slate800)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Connection Indicator Pill
                val (statusColor, statusText) = when {
                    uiState.connectionStatus == ConnectionStatus.Connecting -> Pair(AmberWarning, "Connecting...")
                    uiState.connectionStatus == ConnectionStatus.Connected -> {
                        when (uiState.sessionState.state) {
                            SessionState.Typing -> Pair(BluePrimary, "Typing In Progress")
                            SessionState.Paused -> Pair(AmberWarning, "Typing Paused")
                            SessionState.Completed -> Pair(EmeraldGreen, "Typing Completed")
                            SessionState.Stopped -> Pair(Slate400, "Typing Stopped")
                            SessionState.Ready -> Pair(EmeraldGreen, "Ready & Paired")
                            else -> Pair(EmeraldGreen, "Connected")
                        }
                    }
                    uiState.connectionStatus == ConnectionStatus.Error -> Pair(RedDanger, "Connection Error")
                    else -> Pair(Slate400, "Disconnected")
                }

                Row(
                    modifier = Modifier
                        .clip(RoundedCornerShape(20.dp))
                        .background(Slate900)
                        .padding(horizontal = 12.dp, vertical = 6.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Box(
                        modifier = Modifier
                            .size(8.dp)
                            .clip(CircleShape)
                            .background(statusColor)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = statusText,
                        color = Color.White,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }

                // Pair / Disconnect Action Button
                if (uiState.connectionStatus == ConnectionStatus.Connected) {
                    OutlinedButton(
                        onClick = onDisconnectClicked,
                        shape = RoundedCornerShape(8.dp),
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = RedDanger)
                    ) {
                        Text("Disconnect", fontSize = 12.sp)
                    }
                } else {
                    Button(
                        onClick = onPairClicked,
                        shape = RoundedCornerShape(8.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = BluePrimary)
                    ) {
                        Icon(
                            imageVector = Icons.Default.QrCodeScanner,
                            contentDescription = "Pair",
                            modifier = Modifier.size(16.dp)
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Pair Laptop", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Host & Driver Details
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column {
                    Text(text = "Target Host", color = Slate400, fontSize = 11.sp)
                    Text(
                        text = if (uiState.connectionStatus == ConnectionStatus.Connected)
                            "${uiState.hostInput}:${uiState.portInput}" else "Not connected",
                        color = Color.White,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Medium
                    )
                }

                Column(horizontalAlignment = Alignment.End) {
                    Text(text = "HID Driver", color = Slate400, fontSize = 11.sp)
                    val driverText = when (uiState.driverStatus.state) {
                        DriverState.ActiveEnumerated -> "Genuine KMDF HID"
                        DriverState.DevReceiverFallback -> "Dev Receiver (Test)"
                        else -> if (uiState.connectionStatus == ConnectionStatus.Connected) "Active" else "Offline"
                    }
                    val driverColor = if (uiState.driverStatus.isDevReceiverFallback) AmberWarning else CyanAccent
                    Text(
                        text = driverText,
                        color = driverColor,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
            }
        }
    }
}
