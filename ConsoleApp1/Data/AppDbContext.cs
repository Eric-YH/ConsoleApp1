using Microsoft.EntityFrameworkCore;

namespace ConsoleApp1.Data
{
    using Models;


    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;
        public AppDbContext(string connectionString) => _connectionString = connectionString;

        public DbSet<TransactionRecord> Transactions { get; set; } = null!;
        public DbSet<TransactionAudit> Audits { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionRecord>().HasKey(t => t.TransactionId);
            modelBuilder.Entity<TransactionAudit>().HasKey(a => a.Id);
        }
    }
}
