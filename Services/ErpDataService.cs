using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using P26_002_Pultral.Models;

namespace P26_002_Pultral.Services;

public class ErpDataService
{
    private readonly ClientApiService _client;

    public ErpDataService(ClientApiService client)
    {
        _client = client;
    }

    public async Task<List<ProductItem>> FetchItemsByJobAsync(string jobNumber)
    {
        // 1. Work order
        var woFilter = Uri.EscapeDataString($"Job={jobNumber}");
        var woDoc = await FetchAsync($"data/fetch/workOrderEntity?related=true&limit=100&sort=-CreationDate&filter={woFilter}");
        if (woDoc is null)
            throw new Exception("Incapable de communiquer avec l'ERP (workOrderEntity).");

        var workOrder = GetFirstResult(woDoc);
        if (workOrder is null)
            throw new Exception($"Aucun work order trouve pour le job {jobNumber}.");

        var jobCode = GetString(workOrder.Value, "Job") ?? jobNumber;
        var workOrderNumber = GetString(workOrder.Value, "WorkOrderNumber", "WorkOrder", "Number") ?? jobCode;
        var qty = GetDecimal(workOrder.Value, "PlannedQuantity") ?? 0m;

        // 2. Job entity
        var jobFilter = Uri.EscapeDataString($"Job={jobCode}");
        var jobDoc = await FetchAsync($"data/fetch/jobEntity?limit=1&filter={jobFilter}");
        var job = jobDoc is not null ? GetFirstResult(jobDoc) : null;

        var productCode = job is not null ? GetString(job.Value, "Product") : null;

        if (string.IsNullOrWhiteSpace(productCode))
            throw new Exception("Impossible de determiner le produit pour ce job.");

        var salesOrderNumber = job is not null
            ? GetString(job.Value, "SalesOrder")
            : null;

        var purchaseOrderNumber = job is not null
            ? GetString(job.Value, "PurchaseNumber")
            : null;

        var customerId = job is not null
            ? GetString(job.Value, "CustomerId")
            : null;

        var customerName = job is not null
            ? GetString(job.Value, "CustomerName")
            : null;

        var productSpecification3 = job is not null
            ? GetString(job.Value, "ProductSpecification3") 
            : null;

        var productSpecification4 = job is not null
            ? GetString(job.Value, "ProductSpecification4") 
            : null;

        var productSpecification6 = job is not null
            ? GetString(job.Value, "ProductSpecification6") 
            : null;

        // 3. Product entity
        var productFilter = Uri.EscapeDataString($"Product=\"{productCode}\"");
        var productDoc = await FetchAsync($"data/fetch/productEntity?limit=1&filter={productFilter}");
        var product = productDoc is not null ? GetFirstResult(productDoc) : null;

        // 4. Bill of material
        var bomFilter = Uri.EscapeDataString($"Product=\"{productCode}\"");
        var bomDoc = await FetchAsync($"data/fetch/billOfMaterialEntity?limit=1&filter={bomFilter}");
        var bom = bomDoc is not null ? GetFirstResult(bomDoc) : null;

        var itemCode = bom is not null ? GetString(bom.Value, "ItemCode", "Item") : null;
        var bomDescription = bom is not null ? GetString(bom.Value, "Description1", "Description") : null;

        // 5. Item entity
        string? itemDescription = null;
        string? barRun = null;
        string? barMark = null;

        var tendersRaw = job is not null ? GetString(job.Value, "TendersOfContract") : null;
        if (!string.IsNullOrWhiteSpace(tendersRaw))
        {
            try
            {
                using var tendersDoc = JsonDocument.Parse(tendersRaw);
                barRun = GetString(tendersDoc.RootElement, "BarRun", "Bar Run");
                barMark = GetString(tendersDoc.RootElement, "BarMark", "Bar Mark");
            }
            catch
            {
            }
        }

        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            var itemFilter = Uri.EscapeDataString($"Item=\"{itemCode}\"");
            var itemDoc = await FetchAsync($"data/fetch/itemEntity?limit=1&filter={itemFilter}");
            var item = itemDoc is not null ? GetFirstResult(itemDoc) : null;
            if (item is not null)
            {
                itemDescription = GetString(item.Value, "Description1");
            }
        }

        var resolvedDescription = itemDescription ?? bomDescription;

        var isEdon = false;
        if (!string.IsNullOrWhiteSpace(salesOrderNumber))
        {
            var soFilter = Uri.EscapeDataString($"Code={salesOrderNumber}");
            var soDoc = await FetchAsync($"data/fetch/salesOrderHeaderEntity?limit=1&filter={soFilter}");

            if (soDoc is not null)
            {
                var so = GetFirstResult(soDoc);
                if (so is not null)
                {
                    var soNote = GetString(so.Value, "Note");
                    isEdon = soNote is not null &&
                             soNote.Contains("edon", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return
        [
            new ProductItem
            {
                Description = resolvedDescription ?? string.Empty,
                Item = itemCode ?? string.Empty,
                NoDeJob = jobCode,
                WorkOrderNumber = workOrderNumber,
                ProductCode = productCode,
                ItemDescription = resolvedDescription ?? string.Empty,
                SalesOrderNumber = salesOrderNumber ?? string.Empty,
                PurchaseOrderNumber = purchaseOrderNumber ?? string.Empty,
                CustomerId = customerId ?? string.Empty,
                CustomerName = customerName ?? string.Empty,
                BarRun = barRun ?? string.Empty,
                BarMark = barMark ?? string.Empty,
                ProductSpecification3 = productSpecification3 ?? string.Empty,
                ProductSpecification4 = productSpecification4 ?? string.Empty,
                ProductSpecification6 = productSpecification6 ?? string.Empty,
                QuantiteTotale = (int)qty,
                NbrDeBundles = 1,
                NbrEtiquettes = PickLabelCount((int)qty)
            }
        ];
    }

    private static JsonElement? GetFirstResult(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("Result", out var result) ||
            result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            return null;
        return result[0];
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
            foreach (var name in names)
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind != JsonValueKind.Null)
                    return property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.ToString();
        return null;
    }

    private static decimal? GetDecimal(JsonElement element, params string[] names)
    {
        var value = GetString(element, names);
        return decimal.TryParse(value, out var result) ? result : null;
    }

    private static int PickLabelCount(int total)
    {
        foreach (var candidate in new[] { 10, 5, 4, 2, 1 })
            if (total > 0 && total % candidate == 0)
                return candidate;
        return 1;
    }

    private static string? GetNestedString(JsonElement element, string objectName, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, objectName, StringComparison.OrdinalIgnoreCase) ||
                property.Value.ValueKind != JsonValueKind.Object)
                continue;

            return GetString(property.Value, names);
        }

        return null;
    }

    private async Task<JsonDocument?> FetchAsync(string url)
    {
        try
        {
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var payload = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(payload);
        }
        catch
        {
            return null;
        }
    }
}
