using System;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public class Investment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        // Symbol for Stocks/MFs (e.g., RELIANCE, INFYNSE)
        public string? Symbol { get; set; }
        
        public int InvestmentTypeId { get; set; }
        public InvestmentType? InvestmentType { get; set; }
        
        // Holding details
        public decimal Quantity { get; set; }
        public decimal BuyPrice { get; set; }
        
        // Current value tracking
        public decimal CurrentPrice { get; set; }
        
        // FD specific fields
        public decimal? InterestRate { get; set; }
        public DateTime? MaturityDate { get; set; }
        
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

        // User association
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        // Helper properties for calculations
        public decimal TotalCost => Quantity * BuyPrice;
        public decimal CurrentValue => Quantity * CurrentPrice;
        public decimal ProfitLoss => CurrentValue - TotalCost;
        public decimal ProfitLossPercentage => TotalCost != 0 ? (ProfitLoss / TotalCost) * 100 : 0;
    }
}
