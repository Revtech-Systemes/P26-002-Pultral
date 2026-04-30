using System;

namespace P26_002_Pultral.Models;

public class RfidTagRecord
{
    public int Id { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public string RandomHexPrefix { get; set; } = string.Empty;
    public int LastSerialNumber { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
