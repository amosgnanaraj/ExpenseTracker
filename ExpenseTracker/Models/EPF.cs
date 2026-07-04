using System;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public class EPF
    {
        public int Id { get; set; }
        public string UAN { get; set; } = string.Empty;
        public string? MemberId { get; set; }
        
        public decimal EmployeeContribution { get; set; }
        public decimal EmployerContribution { get; set; }
        public decimal InterestEarned { get; set; }
        public decimal TransferIn { get; set; }
        
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        public decimal CurrentValue => EmployeeContribution + EmployerContribution + InterestEarned + TransferIn;
        public decimal ProfitLoss => EmployerContribution + InterestEarned;
        public decimal ProfitLossPercentage => EmployeeContribution != 0 ? (ProfitLoss / EmployeeContribution) * 100 : 0;
    }
}
