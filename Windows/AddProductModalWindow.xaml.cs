using System;
using System.Collections.Generic;
using System.Windows;
using P26_002_Pultral.Models;
using P26_002_Pultral.Services;

namespace P26_002_Pultral.Windows;

public partial class AddProductModalWindow : Window
{
    public List<ProductItem>? FetchedItems { get; private set; }

    public AddProductModalWindow()
    {
        InitializeComponent();
    }

    private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var jobNumber = ProductNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(jobNumber))
        {
            SnackbarService.ShowError("Veuillez saisir un numero de job.");
            return;
        }

        ConfirmButton.IsEnabled = false;
        CancelButton.IsEnabled  = false;
        ConfirmButton.Content   = "Chargement...";

        try
        {
            FetchedItems  = await App.ErpData.FetchItemsByJobAsync(jobNumber);
            DialogResult  = true;
            Close();
        }
        catch (Exception ex)
        {
            SnackbarService.ShowError(ex.Message);
            ConfirmButton.Content   = "Confirmer";
            ConfirmButton.IsEnabled = true;
            CancelButton.IsEnabled  = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ManualInputButton_Click(object sender, RoutedEventArgs e)
    {
        // Close this modal
        DialogResult = false;
        Close();

        // Open the manual input modal
        var manualInputWindow = new ManualInputModalWindow();
        manualInputWindow.Owner = this.Owner;
        manualInputWindow.ShowDialog();
    }
}
