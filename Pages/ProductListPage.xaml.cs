using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using P26_002_Pultral.Models;
using P26_002_Pultral.Services;
using P26_002_Pultral.Windows;

namespace P26_002_Pultral.Pages;

public partial class ProductListPage : Page
{
    private readonly ObservableCollection<ProductItem> _allProducts = [];

    public ProductListPage()
    {
        InitializeComponent();

        foreach (var item in App.Db.LoadAll())
            _allProducts.Add(item);

        ProductsListBox.ItemsSource = _allProducts;
        Unloaded += (_, _) => App.Db.SaveAll(_allProducts);
    }

    public void ClearAllProducts()
    {
        var clearedCount = _allProducts.Count;
        _allProducts.Clear();
        App.Db.SaveAll(_allProducts);
        App.Logs.Add($"Cleared {clearedCount} product(s) from the list.");
        SnackbarService.ShowInfo($"{clearedCount} produit(s) supprime(s).");
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(ProductsListBox.ItemsSource);
        view.Filter = item =>
        {
            if (item is not ProductItem product)
                return false;

            return string.IsNullOrWhiteSpace(searchText) ||
                   product.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   product.Item.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   product.NoDeJob.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void AddProductsButton_Click(object sender, RoutedEventArgs e)
    {
        var ownerWindow = Window.GetWindow(this);
        var modal = new AddProductModalWindow
        {
            Owner = ownerWindow
        };

        if (modal.ShowDialog() == true && modal.FetchedItems is { Count: > 0 })
        {
            foreach (var item in modal.FetchedItems)
                _allProducts.Add(item);

            App.Db.SaveAll(_allProducts);
            SnackbarService.ShowInfo($"{modal.FetchedItems.Count} produit(s) ajoute(s).");
        }
    }

    private void ValidatePrintsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new ValidateImpressionPage(_allProducts));
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsListBox.SelectedItem is null)
        {
            SnackbarService.ShowError("Selectionnez un produit avant l'impression.");
            return;
        }

        if (ProductsListBox.SelectedItem is not ProductItem product)
            return;

        try
        {
            var label = BuildLabelData(product);
            var epc = await App.Printer.PrintRfidAsync("127.0.0.1", 9100, label);
            SnackbarService.ShowInfo($"ZPL RFID genere pour {product.Item} ({epc}).");
        }
        catch (Exception ex)
        {
            App.Logs.Add($"Print failed for item '{product.Item}': {ex}");
            SnackbarService.ShowError("Erreur pendant la generation de l'impression.");
        }
    }

    private static LabelData BuildLabelData(ProductItem product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var dimension = string.IsNullOrWhiteSpace(product.ProductSpecification6)
            ? $"{product.NbrDeBundles} bundle / {product.NbrEtiquettes} etiquettes"
            : product.ProductSpecification6;

        return new LabelData
        {
            JobNumber = product.NoDeJob,
            WorkOrderNumber = string.IsNullOrWhiteSpace(product.WorkOrderNumber) ? product.NoDeJob : product.WorkOrderNumber,
            SalesOrderNumber = product.SalesOrderNumber,
            PurchaseOrderNumber = product.PurchaseOrderNumber,
            ProductCode = string.IsNullOrWhiteSpace(product.ProductCode) ? product.Item : product.ProductCode,
            ProductDescription = product.Description,
            ItemCode = product.Item,
            ItemDescription = string.IsNullOrWhiteSpace(product.ItemDescription) ? product.Description : product.ItemDescription,
            ProductSpecification3 = product.ProductSpecification3,
            ProductSpecification4 = product.ProductSpecification4,
            ProductSpecification6 = product.ProductSpecification6,
            BarRun = product.BarRun,
            BarMark = product.BarMark,
            Dimension = dimension,
            QuantityProduced = product.QuantiteTotale,
            ManufacturedOn = DateTime.Today,
            CustomerId = product.CustomerId,
            CustomerName = product.CustomerName
        };
    }

    private void ConnectionTestButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}
