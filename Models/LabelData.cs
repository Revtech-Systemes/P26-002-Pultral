using System;

namespace P26_002_Pultral.Models;

public class LabelData
{
    public string JobNumber { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string SalesOrderNumber { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string ProductSpecification3 { get; set; } = string.Empty;
    public string ProductSpecification4 { get; set; } = string.Empty;
    public string ProductSpecification6 { get; set; } = string.Empty;
    public string BarRun { get; set; } = string.Empty;
    public string BarMark { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public int RfidSerialNumber { get; set; }
    public string RfidEpc { get; set; } = string.Empty;
    public decimal QuantityProduced { get; set; }
    public DateTime ManufacturedOn { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}
