using System;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public class MutualFund
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string? FolioNumber { get; set; }
        
        public decimal Units { get; set; }
        public string? Category { get; set; }
        public string? SubCategory { get; set; }
        public decimal AvgNAV { get; set; }
        public decimal CurrentNAV { get; set; }
        
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        public decimal TotalCost => Units * AvgNAV;
        public decimal CurrentValue => Units * CurrentNAV;
        public decimal ProfitLoss => CurrentValue - TotalCost;
    }
}
