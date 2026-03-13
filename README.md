
# Project Structure

```
# Project Structure
Data/
  AppDbContext.cs
Models/
  IncomingTransaction.cs
  TransactionRecord.cs
  TransactionAudit.cs
Services/
  IngestionRunner.cs
Program.cs
appsettings.json
snapshot.json

# File Explanation
Data/
  AppDbContext.cs
    Acts as the bridge between the application and the SQLite database.
    Inherits from DbContext
    Uses DbSet<T> to define tables
    optionsBuilder.UseSqlite(_connectionString); create database

Models/
  IncomingTransaction.cs
    class for json transactions
    Used for reading snapshot.json
  
  TransactionRecord.cs
    class for Database entity
  
  TransactionAudit.cs
    class for Audit table entity
    _db.Audits.Add(...)
    Stored in the same SQLite database file, but as a separate table

Services/
  IngestionRunner.cs
    Main logic layer. Create instance for the classes and operate
    private LoadSnapshot()
      read snapshot.json -> IncomingTransaction
    run()
      _db.SaveChanges()
      Interact with Database
    
Program.cs
  Application entry point
  loads appsettings.json
  works as Main(string[] args), but written in modern simplied way
  
appsettings.json
  settings about
    "Default": "Data Source=ingestion.db"
    "File": "snapshot.json"

snapshot.json
  use for transactions
```

# Core Logic
```
# Core Logic

24-Hour Window
  var now = DateTime.UtcNow;
  var cutoff24 = now.AddHours(-24);
  if >= cutoff24 then it is recent time within 24h

Read snapshot.json
  snapshot.json -> List -> Dictionary

Upsert (by TransactionId)
  For each transaction in snapshot:
    foreach (var tx in snapshot)
  Update only if values changed

Revocation
  Transactions within last 24 hours in DB but missing from snapshot are marked as revoked.
  var candidates = _db.Transactions.Where(r => r.TransactionTime >= cutoff24).ToList();

Finalization
  Transactions older than 24 hours and not finalized are marked with FinalizedUtc
  var toFinalize = _db.Transactions.Where(r => r.TransactionTime < cutoff24 && r.FinalizedUtc == null).ToList();

Idempotency
  Running the program multiple times with the same snapshot does not create duplicates.
  for each:
    var changed = existing.Amount != tx.Amount ...
  Guaranteed by audit only on real changes
```

