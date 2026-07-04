using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    // Temporary utility to fix shifted transactions
    public class DataCorrectionController : Controller
    {
        private readonly ExpenseTrackerDbContext _context;
        public DataCorrectionController(ExpenseTrackerDbContext context) => _context = context;

        public async Task<string> FixShifts()
        {
            // Find transactions that are between 6 PM and midnight UTC (typical of IST shift)
            // and shift them to the next day's midnight UTC.
            var shifted = await _context.Transactions
                .Where(t => t.Date.Hour >= 18)
                .ToListAsync();

            int count = 0;
            foreach (var t in shifted)
            {
                // Normalize to the next day's midnight
                t.Date = DateTime.SpecifyKind(t.Date.AddDays(1).Date, DateTimeKind.Utc);
                count++;
            }

            await _context.SaveChangesAsync();
            return $"Successfully corrected {count} transactions.";
        }
    }
}
