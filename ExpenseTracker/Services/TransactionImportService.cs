using ExcelDataReader;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ExpenseTracker.Services
{
    public interface ITransactionImportService
    {
        Task<(int importedCount, string? error)> ImportStatementAsync(Stream fileStream, string userId, int accountId, string? password = null);
    }

    public class TransactionImportService : ITransactionImportService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly ILogger<TransactionImportService> _logger;

        public TransactionImportService(ExpenseTrackerDbContext context, ILogger<TransactionImportService> logger)
        {
            _context = context;
            _logger = logger;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public async Task<(int importedCount, string? error)> ImportStatementAsync(Stream fileStream, string userId, int accountId, string? password = null)
        {
            try
            {
                var account = await _context.AccountSources.FirstOrDefaultAsync(a => a.Id == accountId && (a.UserId == userId || userId == "Admin"));
                if (account == null) return (0, "Account not found or access denied.");

                var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name.Contains("Misc") || c.Name.Contains("Other")) 
                                    ?? await _context.Categories.FirstOrDefaultAsync();
                
                if (defaultCategory == null) return (0, "No categories found in the system.");

                using var reader = ExcelReaderFactory.CreateReader(fileStream, new ExcelReaderConfiguration
                {
                    Password = password
                });

                var result = reader.AsDataSet();
                if (result.Tables.Count == 0) return (0, "No worksheets found.");

                var table = result.Tables[0];
                int importedCount = 0;
                var transactions = new List<Transaction>();

                // Bank Specific Configuration
                int startRowIndex = 18; // Default to SBI Row 19 (Index 18)
                int dateCol = 0;
                int descCol = 1;
                int debitCol = 3;
                int creditCol = 4;

                if (accountId == 1) // Axis Bank
                {
                    startRowIndex = 17; // Row 18
                    dateCol = 1;        // Column B
                    descCol = 3;        // Column D
                    debitCol = 4;       // Column E
                    creditCol = 5;      // Column F
                }
                else if (accountId == 2) // SBI
                {
                    startRowIndex = 18; // Row 19
                    dateCol = 0;        // Column A
                    descCol = 1;        // Column B
                    debitCol = 3;       // Column D
                    creditCol = 4;      // Column E
                }
                else
                {
                    return (0, "This bank format is not yet supported.");
                }

                for (int i = startRowIndex; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    var dateObj = row[dateCol];
                    
                    if (dateObj == null || string.IsNullOrWhiteSpace(dateObj.ToString())) break;

                    if (!TryParseDate(dateObj, out DateTime date))
                    {
                        _logger.LogWarning("Failed to parse date at row {RowNum} for Account {AccountId}", i + 1, accountId);
                        continue;
                    }

                    decimal debit = TryParseDecimal(row[debitCol]);
                    decimal credit = TryParseDecimal(row[creditCol]);

                    decimal amount = 0;
                    TransactionType type = TransactionType.Debit;

                    if (credit > 0)
                    {
                        amount = credit;
                        type = TransactionType.Credit;
                    }
                    else if (debit > 0)
                    {
                        amount = debit;
                        type = TransactionType.Debit;
                    }
                    else
                    {
                        continue;
                    }

                    var description = row[descCol]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(description)) description = "Imported Statement";

                    var transaction = new Transaction
                    {
                        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                        Amount = amount,
                        Type = type,
                        Description = description,
                        AccountSourceId = accountId,
                        CategoryId = defaultCategory.Id,
                        UserId = account.UserId
                    };

                    transactions.Add(transaction);

                    // Update account balance
                    bool isLiability = account.Type == AccountType.CreditCard || account.Type == AccountType.Loan;
                    if (type == TransactionType.Credit)
                    {
                        if (isLiability) account.Balance -= amount; else account.Balance += amount;
                    }
                    else
                    {
                        if (isLiability) account.Balance += amount; else account.Balance -= amount;
                    }

                    importedCount++;
                }

                if (transactions.Any())
                {
                    _context.Transactions.AddRange(transactions);
                    await _context.SaveChangesAsync();
                }

                return (importedCount, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing bank statement for account {AccountId}", accountId);
                return (0, $"Error: {ex.Message}");
            }
        }

        private bool TryParseDate(object value, out DateTime date)
        {
            if (value is DateTime dt)
            {
                date = dt;
                return true;
            }
            return DateTime.TryParse(value?.ToString(), out date);
        }

        private decimal TryParseDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            if (decimal.TryParse(value.ToString(), out decimal result)) return result;
            return 0;
        }
    }
}
