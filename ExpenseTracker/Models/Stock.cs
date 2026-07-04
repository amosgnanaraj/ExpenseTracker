using System;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public class Stock
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        
        public decimal Quantity { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        public decimal TotalCost => Quantity * BuyPrice;
        public decimal CurrentValue => Quantity * CurrentPrice;
        public decimal ProfitLoss => CurrentValue - TotalCost;
    }
}
