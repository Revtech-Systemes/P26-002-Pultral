using System;
using System.Windows;
using System.Windows.Controls;
using P26_002_Pultral.Services;

namespace P26_002_Pultral.Pages;

public partial class InternalLogPage : Page
{
    public InternalLogPage()
    {
        InitializeComponent();
        Loaded += InternalLogPage_Loaded;
        Unloaded += InternalLogPage_Unloaded;
    }

    private void InternalLogPage_Loaded(object sender, RoutedEventArgs e)
    {
        App.Logs.Updated += Logs_Updated;
        RefreshLogs();
    }

    private void InternalLogPage_Unloaded(object sender, RoutedEventArgs e)
    {
        App.Logs.Updated -= Logs_Updated;
    }

    private void Logs_Updated(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshLogs);
    }

    private void RefreshLogs()
    {
        LogTextBox.Text = App.Logs.GetText();
        LogTextBox.ScrollToEnd();
    }
}
