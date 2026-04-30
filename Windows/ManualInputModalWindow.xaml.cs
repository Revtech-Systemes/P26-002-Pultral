using System;
using System.Windows;
using P26_002_Pultral.Models;

namespace P26_002_Pultral.Windows;

public partial class ManualInputModalWindow : Window
{
    public ProductItem? CreatedProductItem { get; private set; }

    public ManualInputModalWindow()
    {
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CreatedProductItem = new ProductItem
            {
                Description = DescriptionBox.Text,
                Item = ItemBox.Text,
                NoDeJob = NoDeJobBox.Text,
                WorkOrderNumber = WorkOrderNumberBox.Text,
                ProductCode = ProductCodeBox.Text,
                ItemDescription = ItemDescriptionBox.Text,
                SalesOrderNumber = SalesOrderNumberBox.Text,
                PurchaseOrderNumber = PurchaseOrderNumberBox.Text,
                CustomerId = CustomerIdBox.Text,
                CustomerName = CustomerNameBox.Text,
                BarRun = BarRunBox.Text,
                BarMark = BarMarkBox.Text,
                ProductSpecification3 = ProductSpecification3Box.Text,
                ProductSpecification4 = ProductSpecification4Box.Text,
                ProductSpecification6 = ProductSpecification6Box.Text,
                QuantiteImprimee = int.TryParse(QuantiteImprimeeBox.Text, out var qi) ? qi : 0,
                QuantiteTotale = int.TryParse(QuantiteTotaleBox.Text, out var qt) ? qt : 0,
                NbrDeBundles = int.TryParse(NbrDeBundlesBox.Text, out var nb) ? nb : 0,
                NbrEtiquettes = int.TryParse(NbrEtiquettesBox.Text, out var ne) ? ne : 0,
                QuantiteValidee = int.TryParse(QuantiteValideeBox.Text, out var qv) ? qv : 0
            };

            // Save to database
            var allProducts = App.Db.LoadAll();
            allProducts.Add(CreatedProductItem);
            App.Db.SaveAll(allProducts);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors de la création du produit : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
