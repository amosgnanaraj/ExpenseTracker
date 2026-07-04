using System;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public enum InterestPayoutType
    {
        Monthly,
        Quarterly,
        Annually,
        AtMaturity
    }

    public class FixedDeposit
    {
        public int Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string? CertificateNumber { get; set; }
        
        public decimal PrincipalAmount { get; set; }
        public decimal InterestRate { get; set; }
        public DateTime MaturityDate { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        
        public InterestPayoutType PayoutType { get; set; } = InterestPayoutType.AtMaturity;

        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        public decimal MaturityAmount => PrincipalAmount + (PrincipalAmount * InterestRate / 100 * (decimal)(MaturityDate - PurchaseDate).TotalDays / 365);

        public decimal AccruedInterest
        {
            get
            {
                var today = DateTime.UtcNow;
                if (today >= MaturityDate) return MaturityAmount - PrincipalAmount;
                if (today <= PurchaseDate) return 0;
                return (PrincipalAmount * InterestRate / 100 * (decimal)(today - PurchaseDate).TotalDays / 365);
            }
        }

        public decimal CurrentValue => PrincipalAmount + AccruedInterest;
        public decimal ProfitLoss => AccruedInterest;
        public int DaysToMaturity => Math.Max(0, (int)(MaturityDate - DateTime.UtcNow).TotalDays);
        public bool IsMatured => DateTime.UtcNow >= MaturityDate;
    }
}
