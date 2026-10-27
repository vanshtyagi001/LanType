package com.lantype.app.ui.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.HelpOutline
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Stop
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.R
import com.lantype.app.data.network.ConnectionStatus
import com.lantype.app.ui.components.SpeedControls
import com.lantype.app.ui.components.StatusCard
import com.lantype.app.ui.components.TelemetryCard
import com.lantype.app.ui.components.TextEditorSection
import com.lantype.app.ui.theme.AccentWhite
import com.lantype.app.ui.theme.BlackBg
import com.lantype.app.ui.theme.BorderColor
import com.lantype.app.ui.theme.DangerRed
import com.lantype.app.ui.theme.SurfaceDark
import com.lantype.app.ui.theme.SurfaceElevated
import com.lantype.app.ui.theme.TextMuted
import com.lantype.app.ui.theme.TextPrimary
import com.lantype.app.ui.theme.TextSecondary
import com.lantype.app.ui.viewmodel.MainUiState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    uiState: MainUiState,
    onTextChanged: (String) -> Unit,
    onClearText: () -> Unit,
    onPasteText: (String) -> Unit,
    onUnitChanged: (com.lantype.app.data.model.SpeedUnit) -> Unit,
    onSpeedChanged: (Int) -> Unit,
    onPairClicked: () -> Unit,
    onDisconnectClicked: () -> Unit,
    onHelpClicked: () -> Unit,
    onStartTyping: () -> Unit,
    onPauseTyping: () -> Unit,
    onResumeTyping: () -> Unit,
    onStopTyping: () -> Unit,
    onEmergencyStop: () -> Unit,
    onHostChanged: (String) -> Unit,
    onPortChanged: (String) -> Unit,
    onPinChanged: (String) -> Unit,
    onConnectDialogClicked: () -> Unit,
    onDismissPairingDialog: () -> Unit,
    onDismissHelpDialog: () -> Unit,
    onQrScanned: (String) -> Boolean,
    onClearError: () -> Unit,
    onRescanDevices: () -> Unit = {},
    onReconnectClicked: () -> Unit = {}
) {
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(uiState.errorMessage) {
        uiState.errorMessage?.let { msg ->
            snackbarHostState.showSnackbar(msg)
            onClearError()
        }
    }

    Scaffold(
        contentWindowInsets = WindowInsets.statusBars,
        topBar = {
            CenterAlignedTopAppBar(
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Image(
                            painter = painterResource(id = R.drawable.logo),
                            contentDescription = "Logo",
                            modifier = Modifier
                                .size(26.dp)
                                .clip(RoundedCornerShape(6.dp))
                        )
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = "LANType",
                            color = TextPrimary,
                            fontSize = 17.sp,
                            fontWeight = FontWeight.Bold,
                            letterSpacing = (-0.3).sp
                        )
                    }
                },
                actions = {
                    IconButton(onClick = onHelpClicked) {
                        Icon(
                            imageVector = Icons.Default.HelpOutline,
                            contentDescription = "Help",
                            tint = TextSecondary
                        )
                    }
                },
                colors = TopAppBarDefaults.centerAlignedTopAppBarColors(
                    containerColor = BlackBg
                )
            )
        },
        bottomBar = {
            BottomActionBar(
                uiState = uiState,
                onPairClicked = onPairClicked,
                onStartTyping = onStartTyping,
                onPauseTyping = onPauseTyping,
                onResumeTyping = onResumeTyping,
                onStopTyping = onStopTyping,
                onEmergencyStop = onEmergencyStop
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = BlackBg
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(horizontal = 16.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Spacer(modifier = Modifier.height(2.dp))

            // Connection Error Warning Banner
            // Connection Warning / Auto-Reconnect Banner
            if (uiState.connectionStatus == ConnectionStatus.Error || uiState.connectionStatus == ConnectionStatus.Reconnecting) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(8.dp),
                    border = BorderStroke(1.dp, DangerRed.copy(alpha = 0.4f)),
                    colors = CardDefaults.cardColors(containerColor = SurfaceDark)
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 14.dp, vertical = 10.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Warning,
                                contentDescription = "Warning",
                                tint = DangerRed,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(10.dp))
                            Text(
                                text = if (uiState.connectionStatus == ConnectionStatus.Reconnecting)
                                    "Reconnecting to PC automatically..."
                                else
                                    "Connection lost. Tap to reconnect.",
                                color = DangerRed,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Medium
                            )
                        }

                        Button(
                            onClick = onReconnectClicked,
                            shape = RoundedCornerShape(6.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = SurfaceElevated,
                                contentColor = AccentWhite
                            ),
                            modifier = Modifier.height(32.dp)
                        ) {
                            Text("Reconnect", fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                        }
                    }
                }
            }

            // Status Card
            StatusCard(
                uiState = uiState,
                onPairClicked = onPairClicked,
                onDisconnectClicked = onDisconnectClicked
            )

            // Live Telemetry Card (Shown when typing / paused / characters typed)
            if (uiState.isTyping || uiState.isPaused || uiState.sessionState.charactersTyped > 0) {
                TelemetryCard(uiState = uiState)
            }

            // Text Editor Section
            TextEditorSection(
                uiState = uiState,
                onTextChanged = onTextChanged,
                onClearText = onClearText,
                onPasteText = onPasteText
            )

            // Speed Controls
            SpeedControls(
                uiState = uiState,
                onUnitChanged = onUnitChanged,
                onSpeedChanged = onSpeedChanged
            )

            Spacer(modifier = Modifier.height(16.dp))
        }

        // Dialogs
        if (uiState.isPairingDialogVisible) {
            PairingDialog(
                uiState = uiState,
                onHostChanged = onHostChanged,
                onPortChanged = onPortChanged,
                onPinChanged = onPinChanged,
                onConnectClicked = onConnectDialogClicked,
                onDismiss = onDismissPairingDialog,
                onQrScanned = onQrScanned,
                onRescan = onRescanDevices
            )
        }

        if (uiState.isHelpDialogVisible) {
            HelpDialog(onDismiss = onDismissHelpDialog)
        }
    }
}

@Composable
private fun BottomActionBar(
    uiState: MainUiState,
    onPairClicked: () -> Unit,
    onStartTyping: () -> Unit,
    onPauseTyping: () -> Unit,
    onResumeTyping: () -> Unit,
    onStopTyping: () -> Unit,
    onEmergencyStop: () -> Unit
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(SurfaceDark)
            .drawBehind {
                drawLine(
                    color = BorderColor,
                    start = Offset(0f, 0f),
                    end = Offset(size.width, 0f),
                    strokeWidth = 1.dp.toPx()
                )
            }
            .navigationBarsPadding() // Ensures action bar is never overlapped by system 3-button or gesture bar
            .padding(horizontal = 16.dp, vertical = 12.dp)
    ) {
        when {
            uiState.isTyping -> {
                // Actions while Typing: Pause, Stop, Emergency Killswitch
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedButton(
                        onClick = onPauseTyping,
                        modifier = Modifier.weight(1f).height(48.dp),
                        shape = RoundedCornerShape(8.dp),
                        border = BorderStroke(1.dp, BorderColor),
                        colors = ButtonDefaults.outlinedButtonColors(
                            containerColor = SurfaceElevated,
                            contentColor = TextPrimary
                        )
                    ) {
                        Icon(imageVector = Icons.Default.Pause, contentDescription = "Pause", modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Pause", fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                    }

                    OutlinedButton(
                        onClick = onStopTyping,
                        modifier = Modifier.weight(1f).height(48.dp),
                        shape = RoundedCornerShape(8.dp),
                        border = BorderStroke(1.dp, BorderColor),
                        colors = ButtonDefaults.outlinedButtonColors(
                            containerColor = SurfaceElevated,
                            contentColor = TextPrimary
                        )
                    ) {
                        Icon(imageVector = Icons.Default.Stop, contentDescription = "Stop", modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Stop", fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                    }

                    Button(
                        onClick = onEmergencyStop,
                        modifier = Modifier.height(48.dp),
                        shape = RoundedCornerShape(8.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = DangerRed,
                            contentColor = AccentWhite
                        )
                    ) {
                        Text("Kill", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }

            uiState.isPaused -> {
                // Actions while Paused: Resume, Stop
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Button(
                        onClick = onResumeTyping,
                        modifier = Modifier.weight(1.5f).height(48.dp),
                        shape = RoundedCornerShape(8.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = AccentWhite,
                            contentColor = BlackBg
                        )
                    ) {
                        Icon(imageVector = Icons.Default.PlayArrow, contentDescription = "Resume", modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Resume Typing", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    }

                    OutlinedButton(
                        onClick = onStopTyping,
                        modifier = Modifier.weight(1f).height(48.dp),
                        shape = RoundedCornerShape(8.dp),
                        border = BorderStroke(1.dp, BorderColor),
                        colors = ButtonDefaults.outlinedButtonColors(
                            containerColor = SurfaceElevated,
                            contentColor = TextPrimary
                        )
                    ) {
                        Text("Stop", fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                    }
                }
            }

            else -> {
                // Idle / Ready: High-contrast professional start button
                val isConnected = uiState.connectionStatus == ConnectionStatus.Connected
                val isPaired = uiState.isPaired
                val hasText = uiState.characterCount > 0

                val buttonText = when {
                    !isConnected -> "Connect to Laptop"
                    !isPaired -> "Enter 6-Digit PIN to Pair"
                    !hasText -> "Enter Text to Start Typing"
                    else -> "Start Typing into Laptop"
                }

                // Enabled if user needs to connect/pair, OR if ready to type with entered text
                val canClick = !isConnected || !isPaired || hasText

                Button(
                    onClick = {
                        if (!isConnected || !isPaired) {
                            onPairClicked()
                        } else {
                            onStartTyping()
                        }
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    enabled = canClick,
                    shape = RoundedCornerShape(8.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = AccentWhite,
                        contentColor = BlackBg,
                        disabledContainerColor = SurfaceElevated,
                        disabledContentColor = TextMuted
                    )
                ) {
                    Icon(
                        imageVector = Icons.Default.PlayArrow, 
                        contentDescription = "Start", 
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(text = buttonText, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}
