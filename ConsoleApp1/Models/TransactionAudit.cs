using System;
using System.ComponentModel.DataAnnotations;

namespace ConsoleApp1.Models
{
    public class TransactionAudit
    {
        [Key]
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string? OldPayloadJson { get; set; }
        public string NewPayloadJson { get; set; } = null!;
    }
}
