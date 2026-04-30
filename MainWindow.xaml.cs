using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using P26_002_Pultral.Pages;
using P26_002_Pultral.Services;

namespace P26_002_Pultral
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ProductListPage _productListPage;
        private readonly InternalLogPage _internalLogPage;
        private readonly DispatcherTimer _snackbarTimer = new() { Interval = TimeSpan.FromSeconds(3) };
        private object? _pageBeforeInternalLogs;

        public MainWindow()
        {
            InitializeComponent();

            _productListPage = new ProductListPage();
            _internalLogPage = new InternalLogPage();

            SnackbarService.MessageRequested += SnackbarService_MessageRequested;
            _snackbarTimer.Tick += SnackbarTimer_Tick;

            MainFrame.Navigate(_productListPage);
            App.Logs.Add("Application started.");
        }

        protected override void OnClosed(EventArgs e)
        {
            SnackbarService.MessageRequested -= SnackbarService_MessageRequested;
            _snackbarTimer.Tick -= SnackbarTimer_Tick;
            base.OnClosed(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D)
            {
                _productListPage.ClearAllProducts();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.I)
            {
                ToggleInternalLogs();
                e.Handled = true;
            }

            base.OnPreviewKeyDown(e);
        }

        private void ToggleInternalLogs()
        {
            if (ReferenceEquals(MainFrame.Content, _internalLogPage))
            {
                MainFrame.Navigate(_pageBeforeInternalLogs ?? _productListPage);
                App.Logs.Add("Internal log page hidden.");
                return;
            }

            _pageBeforeInternalLogs = MainFrame.Content;
            MainFrame.Navigate(_internalLogPage);
            App.Logs.Add("Internal log page shown.");
        }

        private void SnackbarService_MessageRequested(object? sender, SnackbarMessage e)
        {
            App.Logs.Add($"Snackbar [{e.Type}]: {e.Text}");
            SnackbarText.Text = e.Text;

            switch (e.Type)
            {
                case SnackbarMessageType.Error:
                    SnackbarBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFE5E5"));
                    SnackbarBorder.BorderBrush = (Brush)FindResource("ErrorAlertBrush");
                    break;
                default:
                    SnackbarBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE8F4FF"));
                    SnackbarBorder.BorderBrush = (Brush)FindResource("AlarmBlueBrush");
                    break;
            }

            SnackbarBorder.Visibility = Visibility.Visible;
            _snackbarTimer.Stop();
            _snackbarTimer.Start();
        }

        private void SnackbarTimer_Tick(object? sender, EventArgs e)
        {
            _snackbarTimer.Stop();
            SnackbarBorder.Visibility = Visibility.Collapsed;
        }
    }
}