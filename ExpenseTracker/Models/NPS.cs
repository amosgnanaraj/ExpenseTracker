using System;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public class NPS
    {
        public int Id { get; set; }
        public string SchemeName { get; set; } = string.Empty;
        
        public decimal TotalUnits { get; set; }
        public decimal CurrentNAV { get; set; }
        
        // Keyed in by user as there is no individual Avg NAV
        public decimal TotalInvested { get; set; }
        
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        
        public string? UserId { get; set; }
        public IdentityUser? User { get; set; }

        public decimal CurrentValue => TotalUnits * CurrentNAV;
        public decimal ProfitLoss => CurrentValue - TotalInvested;
    }
}
