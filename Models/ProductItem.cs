using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace P26_002_Pultral.Models;

public class ProductItem : INotifyPropertyChanged
{
    public string Description { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string NoDeJob { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string SalesOrderNumber { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BarRun { get; set; } = string.Empty;
    public string BarMark { get; set; } = string.Empty;
    public string ProductSpecification3 { get; set; } = string.Empty;
    public string ProductSpecification4 { get; set; } = string.Empty;
    public string ProductSpecification6 { get; set; } = string.Empty;
    public int QuantiteImprimee { get; set; }
    public int QuantiteTotale { get; set; }

    public string QuantiteImprimeeDisplay => $"{QuantiteImprimee}/{QuantiteTotale}";

    private int _nbrDeBundles;
    public int NbrDeBundles
    {
        get => _nbrDeBundles;
        set { _nbrDeBundles = value; OnPropertyChanged(); }
    }

    private int _nbrEtiquettes;
    public int NbrEtiquettes
    {
        get => _nbrEtiquettes;
        set { _nbrEtiquettes = value; OnPropertyChanged(); }
    }

    private int _quantiteValidee;
    public int QuantiteValidee
    {
        get => _quantiteValidee;
        set
        {
            _quantiteValidee = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QuantiteValideeDisplay));
            OnPropertyChanged(nameof(IsValidationComplete));
            OnPropertyChanged(nameof(IsValidationOver));
        }
    }

    public string QuantiteValideeDisplay => $"{QuantiteValidee}/{QuantiteTotale}";
    public bool IsValidationComplete => QuantiteValidee == QuantiteTotale;
    public bool IsValidationOver    => QuantiteValidee >  QuantiteTotale;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
