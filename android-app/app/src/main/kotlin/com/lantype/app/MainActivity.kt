package com.lantype.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import com.lantype.app.ui.screens.MainScreen
import com.lantype.app.ui.theme.LANTypeTheme
import com.lantype.app.ui.viewmodel.MainViewModel

class MainActivity : ComponentActivity() {

    private val viewModel: MainViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            LANTypeTheme {
                val uiState by viewModel.uiState.collectAsState()

                MainScreen(
                    uiState = uiState,
                    onTextChanged = viewModel::onTextChanged,
                    onClearText = viewModel::onClearText,
                    onPasteText = viewModel::onPasteText,
                    onUnitChanged = viewModel::onSpeedUnitChanged,
                    onSpeedChanged = viewModel::onSpeedValueChanged,
                    onPairClicked = { viewModel.showPairingDialog(true) },
                    onDisconnectClicked = viewModel::disconnect,
                    onHelpClicked = { viewModel.showHelpDialog(true) },
                    onStartTyping = viewModel::startTyping,
                    onPauseTyping = viewModel::pauseTyping,
                    onResumeTyping = viewModel::resumeTyping,
                    onStopTyping = viewModel::stopTyping,
                    onEmergencyStop = viewModel::emergencyStop,
                    onHostChanged = viewModel::onHostInputChanged,
                    onPortChanged = viewModel::onPortInputChanged,
                    onPinChanged = viewModel::onPinInputChanged,
                    onConnectDialogClicked = { viewModel.connectAndPair() },
                    onDismissPairingDialog = { viewModel.showPairingDialog(false) },
                    onDismissHelpDialog = { viewModel.showHelpDialog(false) },
                    onQrScanned = viewModel::handleScannedQrCode,
                    onClearError = viewModel::clearError,
                    onRescanDevices = viewModel::rescanDevices,
                    onReconnectClicked = viewModel::reconnect
                )
            }
        }
    }
}
