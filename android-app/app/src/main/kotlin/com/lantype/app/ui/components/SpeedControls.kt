package com.lantype.app.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.data.model.SpeedUnit
import com.lantype.app.ui.theme.BluePrimary
import com.lantype.app.ui.theme.CyanAccent
import com.lantype.app.ui.theme.Slate400
import com.lantype.app.ui.theme.Slate700
import com.lantype.app.ui.theme.Slate800
import com.lantype.app.ui.theme.Slate900
import com.lantype.app.ui.viewmodel.MainUiState

@Composable
fun SpeedControls(
    uiState: MainUiState,
    onUnitChanged: (SpeedUnit) -> Unit,
    onSpeedChanged: (Int) -> Unit,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Slate800)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header: Unit Selector Toggle
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Typing Speed Control",
                    color = Color.White,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold
                )

                // CPM / WPM Segmented Switch
                Row(
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(Slate900)
                        .padding(2.dp)
                ) {
                    UnitToggleChip(
                        title = "CPM",
                        isSelected = uiState.speedUnit == SpeedUnit.Cpm,
                        onClick = { onUnitChanged(SpeedUnit.Cpm) }
                    )
                    UnitToggleChip(
                        title = "WPM",
                        isSelected = uiState.speedUnit == SpeedUnit.Wpm,
                        onClick = { onUnitChanged(SpeedUnit.Wpm) }
                    )
                }
            }

            Spacer(modifier = Modifier.height(14.dp))

            // Speed Display & Estimation
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Bottom
            ) {
                Column {
                    Text(
                        text = "${uiState.speedValue} ${uiState.speedUnit.name}",
                        color = CyanAccent,
                        fontSize = 24.sp,
                        fontWeight = FontWeight.Bold
                    )
                    val msPerChar = when (uiState.speedUnit) {
                        SpeedUnit.Cpm -> 60000 / uiState.speedValue.coerceAtLeast(1)
                        SpeedUnit.Wpm -> 60000 / (uiState.speedValue * 5).coerceAtLeast(1)
                    }
                    Text(
                        text = "≈ $msPerChar ms per character",
                        color = Slate400,
                        fontSize = 11.sp
                    )
                }

                val estDuration = uiState.estimatedTotalDurationSeconds
                val estMinutes = estDuration / 60
                val estSeconds = estDuration % 60
                Text(
                    text = "Est. Total: ${String.format("%02d:%02d", estMinutes, estSeconds)}",
                    color = Slate400,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Medium
                )
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Speed Slider
            val (minVal, maxVal) = when (uiState.speedUnit) {
                SpeedUnit.Cpm -> Pair(30f, 1200f)
                SpeedUnit.Wpm -> Pair(6f, 240f)
            }

            Slider(
                value = uiState.speedValue.toFloat().coerceIn(minVal, maxVal),
                onValueChange = { onSpeedChanged(it.toInt()) },
                valueRange = minVal..maxVal,
                colors = SliderDefaults.colors(
                    thumbColor = BluePrimary,
                    activeTrackColor = BluePrimary,
                    inactiveTrackColor = Slate900
                )
            )

            // Quick Preset Chips
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                val presets = when (uiState.speedUnit) {
                    SpeedUnit.Cpm -> listOf(150, 300, 450, 600)
                    SpeedUnit.Wpm -> listOf(30, 60, 90, 120)
                }

                presets.forEach { preset ->
                    PresetChip(
                        label = "$preset ${uiState.speedUnit.name}",
                        isSelected = uiState.speedValue == preset,
                        onClick = { onSpeedChanged(preset) },
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        }
    }
}

@Composable
private fun UnitToggleChip(
    title: String,
    isSelected: Boolean,
    onClick: () -> Unit
) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(6.dp))
            .background(if (isSelected) BluePrimary else Color.Transparent)
            .clickable { onClick() }
            .padding(horizontal = 12.dp, vertical = 4.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = title,
            color = if (isSelected) Color.White else Slate400,
            fontSize = 12.sp,
            fontWeight = FontWeight.Bold
        )
    }
}

@Composable
private fun PresetChip(
    label: String,
    isSelected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .background(if (isSelected) Slate700 else Slate900)
            .clickable { onClick() }
            .padding(vertical = 6.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = label,
            color = if (isSelected) CyanAccent else Slate400,
            fontSize = 11.sp,
            fontWeight = FontWeight.Medium
        )
    }
}
