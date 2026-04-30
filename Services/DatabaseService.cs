using LiteDB;
using P26_002_Pultral.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace P26_002_Pultral.Services;

public class DatabaseService : IDisposable
{
    private const string CollectionName = "products";
    private const string RfidCollectionName = "rfidTags";

    private readonly LiteDatabase _db;
    private readonly ILiteCollection<ProductItem> _products;
    private readonly ILiteCollection<RfidTagRecord> _rfidTags;
    private readonly object _syncRoot = new();

    public DatabaseService()
    {
        var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "P26-002-Pultral");
        Directory.CreateDirectory(dbDir);

        var mapper = new BsonMapper();
        mapper.Entity<ProductItem>()
            .Ignore(x => x.QuantiteImprimeeDisplay)
            .Ignore(x => x.QuantiteValideeDisplay)
            .Ignore(x => x.IsValidationComplete)
            .Ignore(x => x.IsValidationOver);

        _db = new LiteDatabase(Path.Combine(dbDir, "data.db"), mapper);
        _products = _db.GetCollection<ProductItem>(CollectionName);
        _rfidTags = _db.GetCollection<RfidTagRecord>(RfidCollectionName);
        _rfidTags.EnsureIndex(x => x.ItemNumber, unique: true);
        _rfidTags.EnsureIndex(x => x.RandomHexPrefix, unique: true);
    }

    public List<ProductItem> LoadAll() => [.. _products.FindAll()];

    public void SaveAll(IEnumerable<ProductItem> items)
    {
        _products.DeleteAll();
        _products.InsertBulk(items);
    }

    public RfidTagRecord? GetRfidTag(string itemNumber)
    {
        lock (_syncRoot)
        {
            return _rfidTags.FindOne(x => x.ItemNumber == itemNumber);
        }
    }

    public bool RfidPrefixExists(string randomHexPrefix)
    {
        lock (_syncRoot)
        {
            return _rfidTags.Exists(x => x.RandomHexPrefix == randomHexPrefix);
        }
    }

    public RfidTagRecord ReserveNextRfidTag(string itemNumber, Func<string> randomHexPrefixFactory)
    {
        lock (_syncRoot)
        {
            var existing = _rfidTags.FindOne(x => x.ItemNumber == itemNumber);
            if (existing is null)
            {
                string randomHexPrefix;
                do
                {
                    randomHexPrefix = randomHexPrefixFactory();
                }
                while (_rfidTags.Exists(x => x.RandomHexPrefix == randomHexPrefix));

                existing = new RfidTagRecord
                {
                    ItemNumber = itemNumber,
                    RandomHexPrefix = randomHexPrefix,
                    LastSerialNumber = 1,
                    CreatedOnUtc = DateTime.UtcNow,
                    UpdatedOnUtc = DateTime.UtcNow
                };

                _rfidTags.Insert(existing);
                return existing;
            }

            if (existing.LastSerialNumber >= ushort.MaxValue)
                throw new InvalidOperationException($"No more RFID serial numbers are available for item '{itemNumber}'.");

            existing.LastSerialNumber++;
            existing.UpdatedOnUtc = DateTime.UtcNow;
            _rfidTags.Update(existing);
            return existing;
        }
    }

    public void Dispose() => _db.Dispose();
}
