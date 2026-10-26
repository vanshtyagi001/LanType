package com.lantype.app.ui.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import com.lantype.app.R
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.data.network.ConnectionStatus
import com.lantype.app.ui.components.SpeedControls
import com.lantype.app.ui.components.StatusCard
import com.lantype.app.ui.components.TelemetryCard
import com.lantype.app.ui.components.TextEditorSection
import com.lantype.app.ui.theme.BluePrimary
import com.lantype.app.ui.theme.CyanAccent
import com.lantype.app.ui.theme.RedDanger
import com.lantype.app.ui.theme.Slate700
import com.lantype.app.ui.theme.Slate900
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
    onClearError: () -> Unit
) {
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(uiState.errorMessage) {
        uiState.errorMessage?.let { msg ->
            snackbarHostState.showSnackbar(msg)
            onClearError()
        }
    }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Image(
                            painter = painterResource(id = R.drawable.logo),
                            contentDescription = "Logo",
                            modifier = Modifier
                                .size(28.dp)
                                .clip(RoundedCornerShape(6.dp))
                        )
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = "LANType",
                            color = Color.White,
                            fontSize = 18.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                },
                actions = {
                    IconButton(onClick = onHelpClicked) {
                        Icon(
                            imageVector = Icons.Default.HelpOutline,
                            contentDescription = "Help",
                            tint = CyanAccent
                        )
                    }
                },
                colors = TopAppBarDefaults.centerAlignedTopAppBarColors(
                    containerColor = Slate900
                )
            )
        },
        bottomBar = {
            BottomActionBar(
                uiState = uiState,
                onStartTyping = onStartTyping,
                onPauseTyping = onPauseTyping,
                onResumeTyping = onResumeTyping,
                onStopTyping = onStopTyping,
                onEmergencyStop = onEmergencyStop
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = Slate900
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

            // Connection Loss Warning Banner
            if (uiState.connectionStatus == ConnectionStatus.Error) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(10.dp),
                    colors = CardDefaults.cardColors(containerColor = RedDanger.copy(alpha = 0.2f))
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(imageVector = Icons.Default.Warning, contentDescription = "Warning", tint = RedDanger)
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = "Connection lost. Text and typing progress are preserved. Reconnect to resume seamlessly.",
                            color = Color.White,
                            fontSize = 12.sp,
                            lineHeight = 16.sp
                        )
                    }
                }
            }

            // Status Card
            StatusCard(
                uiState = uiState,
                onPairClicked = onPairClicked,
                onDisconnectClicked = onDisconnectClicked
            )

            // Live Telemetry Card (Shown when connected / active)
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
                onQrScanned = onQrScanned
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
    onStartTyping: () -> Unit,
    onPauseTyping: () -> Unit,
    onResumeTyping: () -> Unit,
    onStopTyping: () -> Unit,
    onEmergencyStop: () -> Unit
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(Slate900)
            .padding(horizontal = 16.dp, vertical = 12.dp)
    ) {
        when {
            uiState.isTyping -> {
                // Actions while Typing: Pause, Stop, Emergency Killswitch
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Button(
                        onClick = onPauseTyping,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = Slate700)
                    ) {
                        Icon(imageVector = Icons.Default.Pause, contentDescription = "Pause", modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Pause", fontWeight = FontWeight.Bold)
                    }

                    Button(
                        onClick = onStopTyping,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = RedDanger)
                    ) {
                        Icon(imageVector = Icons.Default.Stop, contentDescription = "Stop", modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Stop", fontWeight = FontWeight.Bold)
                    }

                    Button(
                        onClick = onEmergencyStop,
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = RedDanger)
                    ) {
                        Text("🚨 Kill", fontWeight = FontWeight.Bold)
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
                        modifier = Modifier.weight(1.5f),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = BluePrimary)
                    ) {
                        Icon(imageVector = Icons.Default.PlayArrow, contentDescription = "Resume", modifier = Modifier.size(20.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Resume Typing", fontWeight = FontWeight.Bold)
                    }

                    Button(
                        onClick = onStopTyping,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = Slate700)
                    ) {
                        Text("Stop", fontWeight = FontWeight.Bold)
                    }
                }
            }

            else -> {
                // Idle / Ready: Start Typing Button
                val canStart = uiState.isConnectedAndReady && uiState.characterCount > 0
                Button(
                    onClick = onStartTyping,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(52.dp),
                    enabled = canStart,
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = BluePrimary,
                        disabledContainerColor = Slate700
                    )
                ) {
                    Icon(imageVector = Icons.Default.PlayArrow, contentDescription = "Start", modifier = Modifier.size(22.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    val buttonText = when {
                        uiState.connectionStatus != ConnectionStatus.Connected -> "Connect & Pair to Start"
                        uiState.characterCount == 0 -> "Enter Text to Start Typing"
                        else -> "Start Typing into Laptop"
                    }
                    Text(text = buttonText, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}
