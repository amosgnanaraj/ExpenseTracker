using System.Collections.Generic;

namespace ExpenseTracker.Models
{
    public enum AccountType
    {
        Bank,
        CreditCard,
        Loan
    }

    public class AccountSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? AccountNumber { get; set; }
        public AccountType Type { get; set; }
        public decimal Balance { get; set; }
        public decimal? InterestRate { get; set; }
        public decimal MinimumPayment { get; set; }

        public string? UserId { get; set; }
        public Microsoft.AspNetCore.Identity.IdentityUser? User { get; set; }
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
