using System;
using System.ComponentModel.DataAnnotations;

namespace ConsoleApp1.Models
{
    public class TransactionRecord
    {
        [Key]
        public int TransactionId { get; set; }
        public DateTime TransactionTime { get; set; }
        public decimal Amount { get; set; }
        public string CardLast4 { get; set; } = null!;
        public string LocationCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = null!;
        public DateTime LastSeenUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? FinalizedUtc { get; set; }
        public bool Revoked { get; set; }
    }
}
