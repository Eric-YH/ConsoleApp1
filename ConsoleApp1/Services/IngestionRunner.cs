using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConsoleApp1.Data;
using ConsoleApp1.Models;

namespace ConsoleApp1.Services
{
    public class IngestionRunner
    {
        private readonly AppDbContext _db;
        private readonly string _snapshotFile;

        public IngestionRunner(AppDbContext db, string snapshotFile)
        {
            _db = db;
            _snapshotFile = snapshotFile;
        }

        public int Run()
        {
            _db.Database.EnsureCreated();

            var now = DateTime.UtcNow;
            var cutoff24 = now.AddHours(-24);

            var snapshot = LoadSnapshot(_snapshotFile, now);
            var incomingById = snapshot.ToDictionary(t => t.TransactionId);

            int inserted = 0, updated = 0, unchanged = 0, revoked = 0, finalized = 0;
            var nowUtc = DateTime.UtcNow;

            foreach (var tx in snapshot)
            {
                var payload = JsonSerializer.Serialize(tx);
                var existing = _db.Transactions.Find(tx.TransactionId);
                if (existing == null)
                {
                    var rec = new TransactionRecord
                    {
                        TransactionId = tx.TransactionId,
                        TransactionTime = tx.TransactionTime,
                        Amount = tx.Amount,
                        CardLast4 = tx.CardLast4,
                        LocationCode = tx.LocationCode,
                        ProductName = tx.ProductName,
                        PayloadJson = payload,
                        CreatedUtc = nowUtc,
                        LastSeenUtc = nowUtc,
                        Revoked = false
                    };
                    _db.Transactions.Add(rec);
                    _db.Audits.Add(new TransactionAudit { TransactionId = rec.TransactionId, ChangedAtUtc = nowUtc, OldPayloadJson = null, NewPayloadJson = payload });
                    inserted++;
                }
                else
                {
                    var changed = existing.Amount != tx.Amount || existing.CardLast4 != tx.CardLast4 || existing.TransactionTime != tx.TransactionTime || existing.LocationCode != tx.LocationCode || existing.ProductName != tx.ProductName || existing.PayloadJson != payload;
                    if (changed)
                    {
                        _db.Audits.Add(new TransactionAudit { TransactionId = existing.TransactionId, ChangedAtUtc = nowUtc, OldPayloadJson = existing.PayloadJson, NewPayloadJson = payload });
                        existing.Amount = tx.Amount;
                        existing.CardLast4 = tx.CardLast4;
                        existing.TransactionTime = tx.TransactionTime;
                        existing.LocationCode = tx.LocationCode;
                        existing.ProductName = tx.ProductName;
                        existing.PayloadJson = payload;
                        existing.LastSeenUtc = nowUtc;
                        existing.Revoked = false;
                        updated++;
                    }
                    else
                    {
                        existing.LastSeenUtc = nowUtc;
                        unchanged++;
                    }
                }
            }

            var candidates = _db.Transactions.Where(r => r.TransactionTime >= cutoff24).ToList();
            foreach (var rec in candidates)
            {
                if (!incomingById.ContainsKey(rec.TransactionId) && !rec.Revoked)
                {
                    var oldPayload = rec.PayloadJson;
                    rec.Revoked = true;
                    rec.LastSeenUtc = nowUtc;
                    _db.Audits.Add(new TransactionAudit { TransactionId = rec.TransactionId, ChangedAtUtc = nowUtc, OldPayloadJson = oldPayload, NewPayloadJson = oldPayload });
                    revoked++;
                }
            }

            var toFinalize = _db.Transactions.Where(r => r.TransactionTime < cutoff24 && r.FinalizedUtc == null).ToList();
            foreach (var rec in toFinalize)
            {
                rec.FinalizedUtc = nowUtc;
                finalized++;
            }

            _db.SaveChanges();

            Console.WriteLine("Ingestion run complete");
            Console.WriteLine($"Now (UTC): {nowUtc:O}");
            Console.WriteLine($"Inserted: {inserted}, Updated: {updated}, Unchanged: {unchanged}, Revoked: {revoked}, Finalized: {finalized}");

            return 0;
        }

        private static List<IncomingTransaction> LoadSnapshot(string snapshotFile, DateTime now)
        {
            if (!File.Exists(snapshotFile))
            {
                var sample = new List<IncomingTransaction>
                {
                    new IncomingTransaction { TransactionId = 100, TransactionTime = now.AddHours(-1), Amount = 12.34m, CardLast4 = "1234", LocationCode = "STO-01", ProductName = "Mouse" },
                    new IncomingTransaction { TransactionId = 101, TransactionTime = now.AddHours(-2), Amount = 5.00m, CardLast4 = "4321", LocationCode = "STO-02", ProductName = "Cable" },
                    new IncomingTransaction { TransactionId = 102, TransactionTime = now.AddHours(-3), Amount = 7.77m, CardLast4 = "1111", LocationCode = "STO-01", ProductName = "Adapter" }
                };

                try { File.WriteAllText(snapshotFile, JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true })); } catch { }
                return sample;
            }

            var txt = File.ReadAllText(snapshotFile);
            using var doc = JsonDocument.Parse(txt);
            var list = new List<IncomingTransaction>();
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                int? txId = null;
                if (el.TryGetProperty("transactionId", out var p) || el.TryGetProperty("TransactionId", out p) || el.TryGetProperty("transaction_id", out p))
                {
                    if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
                        txId = v;
                    else if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var v2))
                        txId = v2;
                }
                else if (el.TryGetProperty("id", out var p2))
                {
                    if (p2.ValueKind == JsonValueKind.Number && p2.TryGetInt32(out var v3))
                        txId = v3;
                    else if (p2.ValueKind == JsonValueKind.String && int.TryParse(p2.GetString(), out var v4))
                        txId = v4;
                }

                if (!txId.HasValue)
                    continue; // skip invalid entries

                decimal amount = 0m;
                if (el.TryGetProperty("amount", out var pa))
                {
                    if (pa.ValueKind == JsonValueKind.Number)
                        amount = pa.GetDecimal();
                    else if (pa.ValueKind == JsonValueKind.String)
                        decimal.TryParse(pa.GetString(), out amount);
                }

                DateTime txTime = DateTime.UtcNow;
                if (el.TryGetProperty("timestamp", out var pt) || el.TryGetProperty("Timestamp", out pt) || el.TryGetProperty("transactionTime", out pt) || el.TryGetProperty("TransactionTime", out pt) || el.TryGetProperty("occurredAtUtc", out pt) || el.TryGetProperty("OccurredAtUtc", out pt))
                {
                    if (pt.ValueKind == JsonValueKind.String && DateTime.TryParse(pt.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                        txTime = dt.ToUniversalTime();
                    else if (pt.ValueKind == JsonValueKind.Number && pt.TryGetInt64(out var unix))
                        txTime = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }

                string cardLast4 = "0000";
                if (el.TryGetProperty("cardLast4", out var pc) || el.TryGetProperty("cardLast_4", out pc) || el.TryGetProperty("card_last4", out pc))
                {
                    cardLast4 = pc.GetString() ?? cardLast4;
                }
                else if (el.TryGetProperty("cardNumber", out var pcn) || el.TryGetProperty("card_number", out pcn) || el.TryGetProperty("pan", out pcn))
                {
                    var pan = pcn.ValueKind == JsonValueKind.String ? pcn.GetString() : pcn.GetRawText();
                    if (!string.IsNullOrEmpty(pan))
                    {
                        var digits = new string(pan.Where(char.IsDigit).ToArray());
                        if (digits.Length >= 4)
                            cardLast4 = digits.Substring(digits.Length - 4);
                        else
                            cardLast4 = digits;
                    }
                }

                string location = string.Empty;
                if (el.TryGetProperty("locationCode", out var pl) || el.TryGetProperty("location", out pl) || el.TryGetProperty("location_code", out pl))
                    location = pl.GetString() ?? string.Empty;

                string product = string.Empty;
                if (el.TryGetProperty("productName", out var pp) || el.TryGetProperty("product_name", out pp) || el.TryGetProperty("product", out pp))
                    product = pp.GetString() ?? string.Empty;

                list.Add(new IncomingTransaction
                {
                    TransactionId = txId.Value,
                    TransactionTime = txTime,
                    Amount = amount,
                    CardLast4 = cardLast4,
                    LocationCode = location,
                    ProductName = product
                });
            }

            return list;
        }
    }
}
