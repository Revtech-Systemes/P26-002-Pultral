using System;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using P26_002_Pultral.Models;

namespace P26_002_Pultral.Services;

public class LabelPrinterService
{
    private const int EpcPrefixHexLength = 20;
    private static readonly Encoding ZplEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly DatabaseService _database;
    private readonly LogBufferService _logs;

    public LabelPrinterService(DatabaseService database, LogBufferService logs)
    {
        _database = database;
        _logs = logs;
    }

    public async Task PrintAsync(string printerIp, int printerPort, LabelData label, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerIp);
        ArgumentNullException.ThrowIfNull(label);

        var zpl = BuildZpl(label);
        _logs.Add($"ZPL built for item '{ResolveItemNumber(label)}':\n{zpl}");
        var bytes = ZplEncoding.GetBytes(zpl);
        // await SendAsync(printerIp, printerPort, bytes, cancellationToken);
        await Task.CompletedTask;
    }

    public async Task<string> PrintRfidAsync(string printerIp, int printerPort, LabelData label, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerIp);
        ArgumentNullException.ThrowIfNull(label);

        var epcHex = GetNextRfidEpc(label);
        var zpl = BuildRfidZpl(label, epcHex);
        _logs.Add($"RFID ZPL built for item '{ResolveItemNumber(label)}' with EPC '{epcHex}':\n{zpl}");
        var bytes = ZplEncoding.GetBytes(zpl);
        // await SendAsync(printerIp, printerPort, bytes, cancellationToken);
        await Task.CompletedTask;
        return epcHex;
    }

    public string GetNextRfidEpc(LabelData label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return GetNextRfidEpc(ResolveItemNumber(label), label);
    }

    public string GetNextRfidEpc(string itemNumber)
        => GetNextRfidEpc(itemNumber, null);

    public async Task SendAsync(string printerIp, int printerPort, byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerIp);
        ArgumentNullException.ThrowIfNull(bytes);

        if (printerPort <= 0 || printerPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(printerPort));

        using var client = new TcpClient();
        await client.ConnectAsync(printerIp, printerPort, cancellationToken);

        await using var stream = client.GetStream();
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static string BuildZpl(LabelData label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var lineBarRunBarMark = string.Join(" - ", new[] { label.BarRun, label.BarMark }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var specLine = string.Join(" - ", new[]
        {
            label.ProductSpecification3,
            label.ProductSpecification6,
            label.ProductSpecification4
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var hhmm = DateTime.Now.ToString("HH:mm");
        var lineSOJWO = string.Join(" - ", new string?[]
        {
            string.IsNullOrWhiteSpace(label.SalesOrderNumber) ? null : $"O{label.SalesOrderNumber}",
            string.IsNullOrWhiteSpace(label.JobNumber) ? null : $"J{label.JobNumber}",
            string.IsNullOrWhiteSpace(label.WorkOrderNumber) ? null : $"WO{label.WorkOrderNumber}",
            hhmm
        }.Where(s => s is not null).Select(s => s!));



        return string.Join("\n", new[]
        {
            "^XA",
            "^CI28",
            "^PW812",
            "^LL406",
            $"^FO28,18^A0N,44,44^FDItem: {EscapeZpl(Truncate(label.ProductCode, 30))}^FS",
            string.Empty,
            $"^FO28,78^A0N,38,38^FD{EscapeZpl(Truncate(lineBarRunBarMark, 30))}^FS",
            $"^FO28,128^A0N,36,36^FD{EscapeZpl(Truncate(label.ProductDescription, 30))}^FS",
            string.Empty,
            $"^FO500,78^A0N,32,32^FDQty: {label.QuantityProduced.ToString("0.###", CultureInfo.InvariantCulture)}^FS",
            $"^FO500,128^A0N,30,30^FDDim: {EscapeZpl(Truncate(label.Dimension, 20))}^FS",
            string.Empty,
            $"^FO500,174^A0N,26,26^FDDate: {label.ManufacturedOn:yyyy-MM-dd}^FS",
            string.Empty,
            $"^FO28,210^A0N,26,26^FD{EscapeZpl(Truncate(specLine, 48))}^FS",
            $"^FO28,252^A0N,34,34^FD{EscapeZpl(Truncate(label.CustomerName, 25))}^FS",
            $"^FO28,292^A0N,32,32^FDPO: {EscapeZpl(Truncate(label.PurchaseOrderNumber, 25))}^FS",
            $"^FO28,332^A0N,28,28^FD{EscapeZpl(Truncate(lineSOJWO, 55))}^FS",
            "^FO28,370^A0N,18,18^FDFor any inquiry please contact (1) 418-335-3202^FS",
            "^XZ"
        });
    }

    public static string BuildRfidZpl(LabelData label, string epcHex)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(epcHex);

        var labelZpl = BuildZpl(label);
        var endIndex = labelZpl.LastIndexOf("^XZ", StringComparison.Ordinal);
        if (endIndex < 0)
            return labelZpl;

        var rfidBlock = string.Join("\n", new[]
        {
            "^RS8",
            $"^RFW,H,2,12,1^FD{epcHex}^FS"
        });

        return labelZpl.Insert(endIndex, rfidBlock + "\n");
    }

    private string GetNextRfidEpc(string itemNumber, LabelData? label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemNumber);

        var record = _database.ReserveNextRfidTag(itemNumber, GenerateRandomHexPrefix);
        var epcHex = BuildEpcHex(record.RandomHexPrefix, record.LastSerialNumber);

        if (label is not null)
        {
            label.RfidSerialNumber = record.LastSerialNumber;
            label.RfidEpc = epcHex;
        }

        return epcHex;
    }

    private static string ResolveItemNumber(LabelData label)
        => !string.IsNullOrWhiteSpace(label.ItemCode) ? label.ItemCode : label.ProductCode;

    private static string GenerateRandomHexPrefix()
    {
        Span<byte> prefixBytes = stackalloc byte[EpcPrefixHexLength / 2];
        RandomNumberGenerator.Fill(prefixBytes);
        return Convert.ToHexString(prefixBytes);
    }

    private static string BuildEpcHex(string randomHexPrefix, int serialNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(randomHexPrefix);

        if (randomHexPrefix.Length != EpcPrefixHexLength)
            throw new ArgumentException($"RFID random prefix must be {EpcPrefixHexLength} hex characters.", nameof(randomHexPrefix));

        if (serialNumber < 0 || serialNumber > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(serialNumber), $"Serial number must be between 0 and {ushort.MaxValue}.");

        return $"{randomHexPrefix}{serialNumber:X4}";
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string EscapeZpl(string value)
        => value.Replace("^", " ").Replace("~", " ");
}
