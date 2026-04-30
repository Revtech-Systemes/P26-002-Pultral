using System;
using System.Collections.Generic;
using System.Globalization;

namespace P26_002_Pultral.Services;

public class LogBufferService
{
    private const int MaxLines = 500;
    private readonly LinkedList<string> _lines = [];
    private readonly object _syncRoot = new();

    public event EventHandler? Updated;

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_syncRoot)
        {
            foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.None))
            {
                _lines.AddLast($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} | {line}");
                while (_lines.Count > MaxLines)
                    _lines.RemoveFirst();
            }
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public string GetText()
    {
        lock (_syncRoot)
        {
            return string.Join(Environment.NewLine, _lines);
        }
    }
}
