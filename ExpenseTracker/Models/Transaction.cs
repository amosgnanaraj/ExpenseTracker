using System;

namespace ExpenseTracker.Models
{
    public enum TransactionType
    {
        Debit,
        Credit,
        Transfer
    }

    public class Transaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public TransactionType Type { get; set; } = TransactionType.Debit;

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int AccountSourceId { get; set; }
        public AccountSource? AccountSource { get; set; }

        // For Transfer type: the destination account
        public int? TransferToAccountSourceId { get; set; }
        public AccountSource? TransferToAccountSource { get; set; }

        public string? UserId { get; set; }
        public Microsoft.AspNetCore.Identity.IdentityUser? User { get; set; }

        public int? SalaryMonth { get; set; }
        public int? SalaryYear { get; set; }
    }
}
