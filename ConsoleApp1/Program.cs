using System;
using System.IO;
using ConsoleApp1.Data;
using ConsoleApp1.Services;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

var config = builder.Build();
var conn = config.GetConnectionString("Default") ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "ingestion.db")}";
var snapshotFile = Path.Combine(AppContext.BaseDirectory, config.GetSection("Snapshot")["File"] ?? "snapshot.json");

using var db = new AppDbContext(conn);
var runner = new IngestionRunner(db, snapshotFile);
return runner.Run();

