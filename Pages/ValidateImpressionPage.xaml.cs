using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using P26_002_Pultral.Models;
using P26_002_Pultral.Services;

namespace P26_002_Pultral.Pages;

public partial class ValidateImpressionPage : Page
{
    private readonly ObservableCollection<ProductItem> _allProducts;
    private readonly ObservableCollection<ProductItem> _currentBill = new();

    public ValidateImpressionPage(ObservableCollection<ProductItem> allProducts)
    {
        InitializeComponent();
        _allProducts = allProducts;
        ProductsListBox.ItemsSource = _currentBill;

        _currentBill.Add(new ProductItem { Description = "Glass fiber rod 4x6",        Item = "ETQ-001", NoDeJob = "J-2024-01", QuantiteImprimee = 50,  QuantiteTotale = 100, QuantiteValidee = 30 });
        _currentBill.Add(new ProductItem { Description = "Glass fiber rod 8x12",        Item = "ETQ-002", NoDeJob = "J-2024-02", QuantiteImprimee = 200, QuantiteTotale = 200, QuantiteValidee = 200 });
        _currentBill.Add(new ProductItem { Description = "Glass fiber round rod",       Item = "ETQ-003", NoDeJob = "J-2024-03", QuantiteImprimee = 75,  QuantiteTotale = 75,  QuantiteValidee = 60 });
        _currentBill.Add(new ProductItem { Description = "Glass fiber flat rod long",   Item = "ETQ-004", NoDeJob = "J-2024-04", QuantiteImprimee = 10,  QuantiteTotale = 150, QuantiteValidee = 180 });
        _currentBill.Add(new ProductItem { Description = "Glass fiber reinforced rod",  Item = "ETQ-005", NoDeJob = "J-2024-05", QuantiteImprimee = 50,  QuantiteTotale = 50,  QuantiteValidee = 45 });
    }

    private void LoadBill()
    {
        var searchText = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
            return;

        _currentBill.Clear();
        foreach (var product in _allProducts.Where(p =>
            p.NoDeJob.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            p.Item.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
        {
            _currentBill.Add(product);
        }

        if (_currentBill.Count == 0)
            SnackbarService.ShowError("Aucun produit trouve pour ce numero.");
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoadBill();
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e) => LoadBill();

    private void RetourButton_Click(object sender, RoutedEventArgs e) => NavigationService.GoBack();

    private void EffacerButton_Click(object sender, RoutedEventArgs e)
    {
        _currentBill.Clear();
        SearchTextBox.Text = string.Empty;
    }
}
