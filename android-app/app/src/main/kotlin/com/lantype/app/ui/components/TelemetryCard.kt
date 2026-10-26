package com.lantype.app.ui.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.ui.theme.AmberWarning
import com.lantype.app.ui.theme.BluePrimary
import com.lantype.app.ui.theme.CyanAccent
import com.lantype.app.ui.theme.PurpleAccent
import com.lantype.app.ui.theme.Slate400
import com.lantype.app.ui.theme.Slate800
import com.lantype.app.ui.theme.Slate900
import com.lantype.app.ui.viewmodel.MainUiState

@Composable
fun TelemetryCard(
    uiState: MainUiState,
    modifier: Modifier = Modifier
) {
    val animatedProgress by animateFloatAsState(
        targetValue = uiState.progressPercent,
        label = "ProgressAnimation"
    )

    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Slate800)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Live Typing Telemetry",
                    color = Color.White,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "${(animatedProgress * 100).toInt()}%",
                    color = CyanAccent,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold
                )
            }

            Spacer(modifier = Modifier.height(10.dp))

            // Progress Bar
            LinearProgressIndicator(
                progress = { animatedProgress },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(10.dp)
                    .clip(RoundedCornerShape(5.dp)),
                color = BluePrimary,
                trackColor = Slate900
            )

            Spacer(modifier = Modifier.height(12.dp))

            // Progress Count
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = "${uiState.sessionState.charactersTyped} of ${uiState.sessionState.totalCharacters} chars",
                    color = Slate400,
                    fontSize = 12.sp
                )
                Text(
                    text = "${uiState.sessionState.charactersRemaining} remaining",
                    color = Slate400,
                    fontSize = 12.sp
                )
            }

            Spacer(modifier = Modifier.height(14.dp))

            // Metrics Grid
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                MetricBox(
                    label = "SPEED",
                    value = "${uiState.sessionState.currentCpm} CPM",
                    color = CyanAccent,
                    modifier = Modifier.weight(1f)
                )

                MetricBox(
                    label = "WORDS",
                    value = "${uiState.sessionState.currentWpm} WPM",
                    color = PurpleAccent,
                    modifier = Modifier.weight(1f)
                )

                val remainingSeconds = uiState.sessionState.estimatedRemainingMilliseconds / 1000
                val timeRemainingStr = String.format("%02d:%02d", remainingSeconds / 60, remainingSeconds % 60)
                MetricBox(
                    label = "REMAINING",
                    value = timeRemainingStr,
                    color = AmberWarning,
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun MetricBox(
    label: String,
    value: String,
    color: Color,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .background(Slate900)
            .padding(vertical = 10.dp, horizontal = 8.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text(text = label, color = Slate400, fontSize = 9.sp, fontWeight = FontWeight.Bold)
            Spacer(modifier = Modifier.height(2.dp))
            Text(text = value, color = color, fontSize = 14.sp, fontWeight = FontWeight.Bold)
        }
    }
}
