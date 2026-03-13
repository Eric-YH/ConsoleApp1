
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
    Inherits from DbContext.
    DbContext is the database control center.
    Uses DbSet<T> to define tables.
    optionsBuilder.UseSqlite(_connectionString); create database.


Models/

  IncomingTransaction.cs
    Class for json transactions.
    Used for reading snapshot.json.

  TransactionRecord.cs
    Class for Database entity.
    Each row in database has a corresponding class in C#.

  TransactionAudit.cs
    Class for Audit table entity.
    _db.Audits.Add(...);
    Stored in the same SQLite database file, but as a separate table.


Services/

  IngestionRunner.cs
    Main logic layer.
    Create instance for the classes and operate.

    private LoadSnapshot()
      Read snapshot.json -> IncomingTransaction list.

    Run()
      _db.SaveChanges();
      Interact with Database.


Program.cs
  Application entry point.
  Loads appsettings.json.
  Works as Main(string[] args), but written in modern simplified way.


appsettings.json
  Settings about:
    "Default": "Data Source=ingestion.db"
    "File": "snapshot.json"


snapshot.json
  Use for transactions.

# Core Logic
24-Hour Window
var now = DateTime.UtcNow;
var cutoff24 = now.AddHours(-24);
if >= cutoff24 then it is recent time within 24h
Read snapchat.json ->  List  -> Dictionary

Upsert (by TransactionId)
For each transaction in snapshot:   foreach (var tx in snapshot)
Update only if values changed

Revocation
Transactions within last 24 hours in DB but missing from snapshot are marked as revoked.
var candidates = _db.Transactions.Where(r => r.TransactionTime >= cutoff24).ToList();

Finalization
Transactions older than 24 hours and not finalized are marked with FinalizedUtc
var toFinalize = _db.Transactions.Where(r => r.TransactionTime < cutoff24 && r.FinalizedUtc == null).ToList();

Idempotency
Running the program multiple times with the same snapshot does not create duplicates.
for each: var changed = existing.Amount != tx.Amount...
Guaranteed by audit only on real changes


