package com.lantype.app.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.data.model.DriverState
import com.lantype.app.data.model.SessionState
import com.lantype.app.data.network.ConnectionStatus
import com.lantype.app.ui.theme.AccentWhite
import com.lantype.app.ui.theme.BlackBg
import com.lantype.app.ui.theme.BorderColor
import com.lantype.app.ui.theme.DangerRed
import com.lantype.app.ui.theme.SuccessGreen
import com.lantype.app.ui.theme.SurfaceDark
import com.lantype.app.ui.theme.TextMuted
import com.lantype.app.ui.theme.TextPrimary
import com.lantype.app.ui.theme.TextSecondary
import com.lantype.app.ui.theme.WarningAmber
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
        shape = RoundedCornerShape(8.dp),
        border = BorderStroke(1.dp, BorderColor),
        colors = CardDefaults.cardColors(containerColor = SurfaceDark)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header: Status Text and Pair/Disconnect Action
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Professional Minimal Status
                val (statusDotColor, statusTitle) = when {
                    uiState.connectionStatus == ConnectionStatus.Connecting -> Pair(WarningAmber, "Connecting...")
                    uiState.connectionStatus == ConnectionStatus.Reconnecting -> Pair(WarningAmber, "Reconnecting...")
                    uiState.connectionStatus == ConnectionStatus.Connected -> {
                        if (!uiState.isPaired) {
                            Pair(WarningAmber, "Connected • Enter PIN")
                        } else {
                            when (uiState.sessionState.state) {
                                SessionState.Typing -> Pair(SuccessGreen, "Typing active")
                                SessionState.Paused -> Pair(WarningAmber, "Paused")
                                SessionState.Completed -> Pair(SuccessGreen, "Completed")
                                SessionState.Stopped -> Pair(TextSecondary, "Stopped")
                                else -> Pair(SuccessGreen, "Connected & Ready")
                            }
                        }
                    }
                    uiState.connectionStatus == ConnectionStatus.Error -> Pair(DangerRed, "Connection Error")
                    else -> Pair(TextMuted, "Disconnected")
                }

                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(text = "●", color = statusDotColor, fontSize = 12.sp)
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = statusTitle,
                        color = TextPrimary,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }

                if (uiState.connectionStatus == ConnectionStatus.Connected && uiState.isPaired) {
                    OutlinedButton(
                        onClick = onDisconnectClicked,
                        shape = RoundedCornerShape(6.dp),
                        border = BorderStroke(1.dp, BorderColor),
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = DangerRed)
                    ) {
                        Text("Disconnect", fontSize = 12.sp, fontWeight = FontWeight.Medium)
                    }
                } else {
                    Button(
                        onClick = onPairClicked,
                        shape = RoundedCornerShape(6.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = AccentWhite,
                            contentColor = BlackBg
                        )
                    ) {
                        Text(
                            text = if (uiState.connectionStatus == ConnectionStatus.Connected) "Enter PIN" else "Pair Laptop",
                            fontSize = 12.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(14.dp))

            // Information Grid
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column {
                    Text(text = "Target Endpoint", color = TextMuted, fontSize = 11.sp)
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = if (uiState.connectionStatus == ConnectionStatus.Connected)
                            "${uiState.hostInput}:${uiState.portInput}" else "Not connected",
                        color = TextPrimary,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Medium
                    )
                }

                Column(horizontalAlignment = Alignment.End) {
                    Text(text = "Driver Stack", color = TextMuted, fontSize = 11.sp)
                    Spacer(modifier = Modifier.height(2.dp))
                    val driverText = when (uiState.driverStatus.state) {
                        DriverState.ActiveEnumerated -> "Virtual HID (KMDF)"
                        DriverState.DevReceiverFallback -> "Dev Receiver (Fallback)"
                        else -> if (uiState.connectionStatus == ConnectionStatus.Connected) "Active" else "Offline"
                    }
                    Text(
                        text = driverText,
                        color = if (uiState.driverStatus.isDevReceiverFallback) WarningAmber else TextSecondary,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
            }
        }
    }
}
