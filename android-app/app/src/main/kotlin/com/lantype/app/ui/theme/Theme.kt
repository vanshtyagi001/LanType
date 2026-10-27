package com.lantype.app.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val DarkColorScheme = darkColorScheme(
    primary = AccentWhite,
    onPrimary = BlackBg,
    primaryContainer = SurfaceElevated,
    onPrimaryContainer = TextPrimary,
    secondary = TextSecondary,
    onSecondary = BlackBg,
    background = BlackBg,
    onBackground = TextPrimary,
    surface = SurfaceDark,
    onSurface = TextPrimary,
    surfaceVariant = SurfaceElevated,
    onSurfaceVariant = TextSecondary,
    error = DangerRed,
    onError = AccentWhite
)

@Composable
fun LANTypeTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = DarkColorScheme,
        typography = Typography,
        content = content
    )
}
