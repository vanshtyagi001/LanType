package com.lantype.app.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.ContentPaste
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lantype.app.ui.theme.AccentWhite
import com.lantype.app.ui.theme.BlackBg
import com.lantype.app.ui.theme.BorderColor
import com.lantype.app.ui.theme.DangerRed
import com.lantype.app.ui.theme.SuccessGreen
import com.lantype.app.ui.theme.SurfaceDark
import com.lantype.app.ui.theme.SurfaceElevated
import com.lantype.app.ui.theme.TextMuted
import com.lantype.app.ui.theme.TextPrimary
import com.lantype.app.ui.theme.TextSecondary
import com.lantype.app.ui.viewmodel.MainUiState

@Composable
fun TextEditorSection(
    uiState: MainUiState,
    onTextChanged: (String) -> Unit,
    onClearText: () -> Unit,
    onPasteText: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    val clipboardManager = LocalClipboardManager.current

    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(8.dp),
        border = BorderStroke(1.dp, BorderColor),
        colors = CardDefaults.cardColors(containerColor = SurfaceDark)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header with Counters and Quick Actions
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column {
                    Text(
                        text = "Text Payload",
                        color = TextPrimary,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text(
                        text = "${uiState.characterCount} characters • ${uiState.lineCount} lines",
                        color = TextMuted,
                        fontSize = 11.sp
                    )
                }

                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    // Paste Button
                    OutlinedButton(
                        onClick = {
                            val clipboardText = clipboardManager.getText()?.text
                            if (!clipboardText.isNullOrEmpty()) {
                                onPasteText(clipboardText)
                            }
                        },
                        shape = RoundedCornerShape(6.dp),
                        border = BorderStroke(1.dp, BorderColor),
                        colors = ButtonDefaults.outlinedButtonColors(
                            containerColor = SurfaceElevated,
                            contentColor = TextPrimary
                        ),
                        contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 10.dp, vertical = 4.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.ContentPaste,
                            contentDescription = "Paste",
                            tint = TextPrimary,
                            modifier = Modifier.size(14.dp)
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Paste", fontSize = 11.sp, fontWeight = FontWeight.Medium)
                    }

                    // Clear Button
                    if (uiState.text.isNotEmpty()) {
                        OutlinedButton(
                            onClick = onClearText,
                            shape = RoundedCornerShape(6.dp),
                            border = BorderStroke(1.dp, BorderColor),
                            colors = ButtonDefaults.outlinedButtonColors(
                                containerColor = SurfaceElevated,
                                contentColor = DangerRed
                            ),
                            contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 10.dp, vertical = 4.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Clear,
                                contentDescription = "Clear",
                                tint = DangerRed,
                                modifier = Modifier.size(14.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Clear", color = DangerRed, fontSize = 11.sp, fontWeight = FontWeight.Medium)
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Real-Time Sync Indicator
            if (uiState.isTyping || (uiState.isPaused && uiState.sessionState.charactersTyped > 0)) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.padding(bottom = 6.dp)
                ) {
                    Text(
                        text = "●",
                        color = if (uiState.isTyping) SuccessGreen else TextSecondary,
                        fontSize = 10.sp
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = if (uiState.isTyping) 
                            "Syncing live to laptop (${uiState.sessionState.charactersTyped}/${uiState.text.length})"
                        else 
                            "Typing paused at char ${uiState.sessionState.charactersTyped}/${uiState.text.length}",
                        color = if (uiState.isTyping) SuccessGreen else TextSecondary,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
            }

            val visualTransformation = remember(
                uiState.sessionState.charactersTyped,
                uiState.isTyping,
                uiState.isPaused
            ) {
                if (uiState.isTyping || uiState.isPaused || uiState.sessionState.charactersTyped > 0) {
                    LiveTypingVisualTransformation(uiState.sessionState.charactersTyped)
                } else {
                    VisualTransformation.None
                }
            }

            // Text Input Box with Live Sync Highlighting
            OutlinedTextField(
                value = uiState.text,
                onValueChange = onTextChanged,
                readOnly = uiState.isTyping,
                visualTransformation = visualTransformation,
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 140.dp, max = 240.dp),
                placeholder = {
                    Text(
                        text = "Paste or type text to stream to your PC via Virtual HID keyboard...",
                        color = TextMuted,
                        fontSize = 13.sp,
                        lineHeight = 18.sp
                    )
                },
                colors = OutlinedTextFieldDefaults.colors(
                    focusedContainerColor = BlackBg,
                    unfocusedContainerColor = BlackBg,
                    focusedBorderColor = AccentWhite,
                    unfocusedBorderColor = BorderColor,
                    focusedTextColor = TextPrimary,
                    unfocusedTextColor = TextPrimary,
                    cursorColor = AccentWhite
                ),
                shape = RoundedCornerShape(6.dp)
            )
        }
    }
}

/**
 * VisualTransformation that dynamically highlights characters already streamed to the PC
 * and visually marks the live real-time typing cursor.
 */
class LiveTypingVisualTransformation(private val charactersTyped: Int) : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText {
        if (text.isEmpty() || charactersTyped <= 0) {
            return TransformedText(text, OffsetMapping.Identity)
        }

        val builder = AnnotatedString.Builder()
        val typedCount = charactersTyped.coerceIn(0, text.length)

        // 1. Typed portion: Styled in muted green showing it has been safely sent to PC
        if (typedCount > 0) {
            builder.pushStyle(
                SpanStyle(
                    color = SuccessGreen.copy(alpha = 0.8f),
                    fontWeight = FontWeight.Normal
                )
            )
            builder.append(text.text.substring(0, typedCount))
            builder.pop()
        }

        // 2. Active cursor character: highlighted block showing exact real-time sync position
        if (typedCount < text.length) {
            builder.pushStyle(
                SpanStyle(
                    color = BlackBg,
                    background = AccentWhite,
                    fontWeight = FontWeight.Bold
                )
            )
            builder.append(text.text.substring(typedCount, typedCount + 1))
            builder.pop()

            // 3. Remaining upcoming text: normal high-contrast text
            builder.pushStyle(
                SpanStyle(
                    color = TextPrimary,
                    fontWeight = FontWeight.Normal
                )
            )
            builder.append(text.text.substring(typedCount + 1))
            builder.pop()
        }

        return TransformedText(builder.toAnnotatedString(), OffsetMapping.Identity)
    }
}
