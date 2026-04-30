using System;

namespace P26_002_Pultral.Services;

public enum SnackbarMessageType
{
    Info,
    Error
}

public sealed class SnackbarMessage
{
    public required string Text { get; init; }
    public SnackbarMessageType Type { get; init; }
}

public static class SnackbarService
{
    public static event EventHandler<SnackbarMessage>? MessageRequested;

    public static void ShowInfo(string message) =>
        MessageRequested?.Invoke(null, new SnackbarMessage { Text = message, Type = SnackbarMessageType.Info });

    public static void ShowError(string message) =>
        MessageRequested?.Invoke(null, new SnackbarMessage { Text = message, Type = SnackbarMessageType.Error });
}
