using System;

namespace ConsoleApp1.Models
{
    public class IncomingTransaction
    {
        public int TransactionId { get; set; }
        public DateTime TransactionTime { get; set; }
        public decimal Amount { get; set; }
        public string CardLast4 { get; set; } = null!;
        public string LocationCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
    }
}
