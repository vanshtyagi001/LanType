package com.lantype.app.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.ui.theme.AccentWhite
import com.lantype.app.ui.theme.BlackBg
import com.lantype.app.ui.theme.BorderColor
import com.lantype.app.ui.theme.SurfaceDark
import com.lantype.app.ui.theme.SurfaceElevated
import com.lantype.app.ui.theme.TextMuted
import com.lantype.app.ui.theme.TextPrimary
import com.lantype.app.ui.theme.TextSecondary
import com.lantype.app.ui.viewmodel.MainUiState

@Composable
fun TelemetryCard(
    uiState: MainUiState,
    modifier: Modifier = Modifier
) {
    val animatedProgress by animateFloatAsState(
        targetValue = uiState.progressPercent,
        animationSpec = tween(durationMillis = 50, easing = LinearEasing),
        label = "Progress"
    )

    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(8.dp),
        border = BorderStroke(1.dp, BorderColor),
        colors = CardDefaults.cardColors(containerColor = SurfaceDark)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Session Progress",
                    color = TextPrimary,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = "${(animatedProgress * 100).toInt()}%",
                    color = TextPrimary,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Bold
                )
            }

            Spacer(modifier = Modifier.height(10.dp))

            // Minimal Progress Line
            LinearProgressIndicator(
                progress = { animatedProgress },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(4.dp)
                    .clip(RoundedCornerShape(2.dp)),
                color = AccentWhite,
                trackColor = SurfaceElevated
            )

            Spacer(modifier = Modifier.height(10.dp))

            // Counter Line
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = "${uiState.sessionState.charactersTyped} of ${uiState.sessionState.totalCharacters} chars typed",
                    color = TextSecondary,
                    fontSize = 11.sp
                )
                Text(
                    text = "${uiState.sessionState.charactersRemaining} remaining",
                    color = TextSecondary,
                    fontSize = 11.sp
                )
            }

            Spacer(modifier = Modifier.height(14.dp))

            // Metrics Row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                MetricItem(
                    label = "SPEED",
                    value = "${uiState.sessionState.currentCpm} CPM",
                    modifier = Modifier.weight(1f)
                )

                MetricItem(
                    label = "WORDS",
                    value = "${uiState.sessionState.currentWpm} WPM",
                    modifier = Modifier.weight(1f)
                )

                val remainingSeconds = uiState.sessionState.estimatedRemainingMilliseconds / 1000
                val timeRemainingStr = String.format("%02d:%02d", remainingSeconds / 60, remainingSeconds % 60)
                MetricItem(
                    label = "REMAINING",
                    value = timeRemainingStr,
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun MetricItem(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(6.dp))
            .background(BlackBg)
            .border(1.dp, BorderColor, RoundedCornerShape(6.dp))
            .padding(vertical = 8.dp, horizontal = 10.dp)
    ) {
        Column {
            Text(text = label, color = TextMuted, fontSize = 9.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(2.dp))
            Text(text = value, color = TextPrimary, fontSize = 13.sp, fontWeight = FontWeight.Bold)
        }
    }
}
