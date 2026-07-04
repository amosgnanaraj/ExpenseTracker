using System.Security.Claims;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class QuickEntryController : Controller
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly ExpenseTracker.Services.ITransactionImportService _importService;
        private readonly IConfiguration _configuration;

        public QuickEntryController(ExpenseTrackerDbContext context, 
            ExpenseTracker.Services.ITransactionImportService importService,
            IConfiguration configuration)
        {
            _context = context;
            _importService = importService;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var viewModel = new QuickEntryViewModel
            {
                Date = DateTime.Now,
                AccountSources = await GetAccountSourcesAsync(userId, isAdmin),
                Categories = await GetGroupedCategoriesAsync(),
                RecentTransactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.AccountSource)
                    .Include(t => t.TransferToAccountSource)
                    .Where(t => isAdmin || t.UserId == userId)
                    .OrderByDescending(t => t.Date)
                    .Take(15)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTransaction([FromForm] QuickEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please correct the errors in the form.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                var accountSource = await _context.AccountSources.FindAsync(model.AccountSourceId);
                if (accountSource == null || (!isAdmin && accountSource.UserId != userId))
                {
                    return Json(new { success = false, message = "Invalid Account Source." });
                }

                var transaction = new Transaction
                {
                    Date = DateTime.SpecifyKind(model.Date.Date, DateTimeKind.Utc),
                    Amount = model.Amount,
                    Description = model.Description,
                    Type = model.Type,
                    CategoryId = model.CategoryId,
                    AccountSourceId = model.AccountSourceId,
                    TransferToAccountSourceId = model.Type == TransactionType.Transfer ? model.TransferToAccountSourceId : null,
                    UserId = accountSource.UserId,
                    SalaryMonth = model.SalaryMonth,
                    SalaryYear = model.SalaryYear
                };

                _context.Transactions.Add(transaction);

                // Account-type-aware balance update
                // Bank: balance is positive (money you have). Credit adds, Debit subtracts.
                // CreditCard/Loan: balance is positive (debt you owe). Debit adds (more debt), Credit subtracts (less debt).
                bool isLiability = accountSource.Type == AccountType.CreditCard 
                                || accountSource.Type == AccountType.Loan;

                switch (model.Type)
                {
                    case TransactionType.Credit:
                        if (isLiability)
                            accountSource.Balance -= model.Amount; // payment reduces debt
                        else
                            accountSource.Balance += model.Amount; // deposit increases bank balance
                        break;
                    case TransactionType.Debit:
                        if (isLiability)
                            accountSource.Balance += model.Amount; // purchase increases debt
                        else
                            accountSource.Balance -= model.Amount; // expense decreases bank balance
                        break;
                    case TransactionType.Transfer:
                        // Transfer always deducts from source, adds to destination
                        if (isLiability)
                            accountSource.Balance += model.Amount; // paying from credit card increases debt
                        else
                            accountSource.Balance -= model.Amount; // transfer out decreases bank balance
                        
                        if (model.TransferToAccountSourceId.HasValue)
                        {
                            var targetAccount = await _context.AccountSources.FindAsync(model.TransferToAccountSourceId.Value);
                            if (targetAccount != null)
                            {
                                bool targetIsLiability = targetAccount.Type == AccountType.CreditCard 
                                                      || targetAccount.Type == AccountType.Loan;
                                if (targetIsLiability)
                                    targetAccount.Balance -= model.Amount; // receiving funds reduces debt
                                else
                                    targetAccount.Balance += model.Amount; // receiving funds increases bank balance
                            }
                        }
                        break;
                }

                await _context.SaveChangesAsync();
                
                // Fetch the fully populated object for returning to the view
                var fetchedTransaction = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.AccountSource)
                    .Include(t => t.TransferToAccountSource)
                    .FirstOrDefaultAsync(t => t.Id == transaction.Id);

                return Json(new 
                { 
                    success = true, 
                    message = "Transaction logged successfully!",
                    transaction = new {
                        date = fetchedTransaction.Date.ToLocalTime().ToString("MMM dd, yyyy"),
                        amount = fetchedTransaction.Amount.ToString("C"),
                        type = fetchedTransaction.Type.ToString(),
                        categoryName = fetchedTransaction.Category?.Name,
                        accountName = fetchedTransaction.AccountSource?.Name,
                        transferToName = fetchedTransaction.TransferToAccountSource?.Name ?? "",
                        description = fetchedTransaction.Description ?? ""
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while saving the transaction." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionsPage(
            int page = 1, 
            int pageSize = 15, 
            string? searchTerm = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int? categoryId = null,
            int? accountId = null,
            TransactionType? type = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                if (string.IsNullOrEmpty(userId) && !isAdmin)
                {
                    return Json(new { rows = new List<object>(), totalCount = 0, page, pageSize, error = "User not identified" });
                }

                var query = _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.AccountSource)
                    .Include(t => t.TransferToAccountSource)
                    .Where(t => isAdmin || t.UserId == userId);

                // Apply Filters
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(t => t.Description != null && t.Description.ToLower().Contains(searchTerm.ToLower()));
                }

                if (dateFrom.HasValue)
                {
                    var fromUtc = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
                    query = query.Where(t => t.Date >= fromUtc);
                }

                if (dateTo.HasValue)
                {
                    var toUtc = DateTime.SpecifyKind(dateTo.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
                    query = query.Where(t => t.Date <= toUtc);
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(t => t.CategoryId == categoryId.Value);
                }

                if (accountId.HasValue && accountId.Value > 0)
                {
                    query = query.Where(t => t.AccountSourceId == accountId.Value || t.TransferToAccountSourceId == accountId.Value);
                }

                if (type.HasValue)
                {
                    query = query.Where(t => t.Type == type.Value);
                }

                // Sorting and Pagination
                query = query.OrderByDescending(t => t.Date);

                var totalCount = await query.CountAsync();
                var transactions = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var rows = transactions.Select(t => {
                    bool isSalary = t.Category?.Name?.Contains("Salary", StringComparison.OrdinalIgnoreCase) ?? false;
                    return new {
                        id = t.Id,
                        date = t.Date.ToLocalTime().ToString("MMM dd, yyyy"),
                        amount = isSalary ? "*****" : t.Amount.ToString("C"),
                        type = t.Type.ToString(),
                        categoryName = t.Category?.Name ?? "",
                        accountName = t.AccountSource?.Name ?? "",
                        transferToName = t.TransferToAccountSource?.Name ?? "",
                        description = t.Description ?? "",
                        isSalary = isSalary,
                        salaryMonth = t.SalaryMonth,
                        salaryYear = t.SalaryYear
                    };
                });

                return Json(new { rows, totalCount, page, pageSize });
            }
            catch (Exception ex)
            {
                return Json(new { rows = new List<object>(), totalCount = 0, page, pageSize, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTransaction(int id, string? pin = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var t = await _context.Transactions
                .Include(x => x.Category)
                .Include(x => x.AccountSource)
                .Include(x => x.TransferToAccountSource)
                .FirstOrDefaultAsync(x => x.Id == id && (isAdmin || x.UserId == userId));

            if (t == null) return NotFound();

            bool isSalary = t.Category?.Name?.Contains("Salary", StringComparison.OrdinalIgnoreCase) ?? false;
            var configPin = _configuration["Security:SalaryPin"] ?? "1234";
            bool isMasked = isSalary && pin != configPin;

            return Json(new {
                id = t.Id,
                date = t.Date.ToLocalTime().ToString("yyyy-MM-dd"),
                amount = isMasked ? 0 : t.Amount,
                isMasked = isMasked,
                isSalary = isSalary,
                type = (int)t.Type,
                categoryId = t.CategoryId,
                accountSourceId = t.AccountSourceId,
                transferToAccountSourceId = t.TransferToAccountSourceId,
                description = t.Description,
                salaryMonth = t.SalaryMonth,
                salaryYear = t.SalaryYear
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTransaction(int id, [FromForm] QuickEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Form invalid", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                var tOld = await _context.Transactions.FindAsync(id);
                if (tOld == null || (!isAdmin && tOld.UserId != userId))
                    return Json(new { success = false, message = "Transaction not found." });

                // Reverse old transaction balance impact
                var oldSrc = await _context.AccountSources.FindAsync(tOld.AccountSourceId);
                if (oldSrc != null)
                {
                    bool oldLiab = oldSrc.Type == AccountType.CreditCard || oldSrc.Type == AccountType.Loan;
                    switch (tOld.Type)
                    {
                        case TransactionType.Credit:
                            if (oldLiab) oldSrc.Balance += tOld.Amount; else oldSrc.Balance -= tOld.Amount;
                            break;
                        case TransactionType.Debit:
                            if (oldLiab) oldSrc.Balance -= tOld.Amount; else oldSrc.Balance += tOld.Amount;
                            break;
                        case TransactionType.Transfer:
                            if (oldLiab) oldSrc.Balance -= tOld.Amount; else oldSrc.Balance += tOld.Amount;
                            if (tOld.TransferToAccountSourceId.HasValue)
                            {
                                var oldTar = await _context.AccountSources.FindAsync(tOld.TransferToAccountSourceId.Value);
                                if (oldTar != null) {
                                    bool tarLiab = oldTar.Type == AccountType.CreditCard || oldTar.Type == AccountType.Loan;
                                    if (tarLiab) oldTar.Balance += tOld.Amount; else oldTar.Balance -= tOld.Amount;
                                }
                            }
                            break;
                    }
                }

                // Apply new properties
                var newSrc = await _context.AccountSources.FindAsync(model.AccountSourceId);
                if (newSrc == null || (!isAdmin && newSrc.UserId != userId))
                    return Json(new { success = false, message = "Invalid Account Source." });

                tOld.Date = DateTime.SpecifyKind(model.Date.Date, DateTimeKind.Utc);
                tOld.Amount = model.Amount;
                tOld.Description = model.Description;
                tOld.Type = model.Type;
                tOld.CategoryId = model.CategoryId;
                tOld.AccountSourceId = model.AccountSourceId;
                tOld.TransferToAccountSourceId = model.Type == TransactionType.Transfer ? model.TransferToAccountSourceId : null;
                tOld.SalaryMonth = model.SalaryMonth;
                tOld.SalaryYear = model.SalaryYear;

                // Apply new balance impact
                bool newLiab = newSrc.Type == AccountType.CreditCard || newSrc.Type == AccountType.Loan;
                switch (model.Type)
                {
                    case TransactionType.Credit:
                        if (newLiab) newSrc.Balance -= model.Amount; else newSrc.Balance += model.Amount;
                        break;
                    case TransactionType.Debit:
                        if (newLiab) newSrc.Balance += model.Amount; else newSrc.Balance -= model.Amount;
                        break;
                    case TransactionType.Transfer:
                        if (newLiab) newSrc.Balance += model.Amount; else newSrc.Balance -= model.Amount;
                        if (model.TransferToAccountSourceId.HasValue)
                        {
                            var newTar = await _context.AccountSources.FindAsync(model.TransferToAccountSourceId.Value);
                            if (newTar != null) {
                                bool tarLiab = newTar.Type == AccountType.CreditCard || newTar.Type == AccountType.Loan;
                                if (tarLiab) newTar.Balance -= model.Amount; else newTar.Balance += model.Amount;
                            }
                        }
                        break;
                }

                await _context.SaveChangesAsync();

                var f = await _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.AccountSource)
                    .Include(t => t.TransferToAccountSource)
                    .FirstOrDefaultAsync(t => t.Id == tOld.Id);

                bool isSalary = f.Category?.Name?.Contains("Salary", StringComparison.OrdinalIgnoreCase) ?? false;
                return Json(new 
                { 
                    success = true, 
                    message = "Transaction updated!",
                    transaction = new {
                        id = f.Id,
                        date = f.Date.ToLocalTime().ToString("MMM dd, yyyy"),
                        amount = isSalary ? "*****" : f.Amount.ToString("C"),
                        type = f.Type.ToString(),
                        categoryName = f.Category?.Name,
                        accountName = f.AccountSource?.Name,
                        transferToName = f.TransferToAccountSource?.Name ?? "",
                        description = f.Description ?? "",
                        isSalary = isSalary,
                        salaryMonth = f.SalaryMonth,
                        salaryYear = f.SalaryYear
                    }
                });
            }
            catch
            {
                return Json(new { success = false, message = "Error saving." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                var t = await _context.Transactions.FindAsync(id);
                if (t == null || (!isAdmin && t.UserId != userId))
                    return Json(new { success = false, message = "Transaction not found." });

                // Reverse balance impact
                var src = await _context.AccountSources.FindAsync(t.AccountSourceId);
                if (src != null)
                {
                    bool isLiab = src.Type == AccountType.CreditCard || src.Type == AccountType.Loan;
                    switch (t.Type)
                    {
                        case TransactionType.Credit:
                            if (isLiab) src.Balance += t.Amount; else src.Balance -= t.Amount;
                            break;
                        case TransactionType.Debit:
                            if (isLiab) src.Balance -= t.Amount; else src.Balance += t.Amount;
                            break;
                        case TransactionType.Transfer:
                            if (isLiab) src.Balance -= t.Amount; else src.Balance += t.Amount;
                            if (t.TransferToAccountSourceId.HasValue)
                            {
                                var tar = await _context.AccountSources.FindAsync(t.TransferToAccountSourceId.Value);
                                if (tar != null) {
                                    bool tarLiab = tar.Type == AccountType.CreditCard || tar.Type == AccountType.Loan;
                                    if (tarLiab) tar.Balance += t.Amount; else tar.Balance -= t.Amount;
                                }
                            }
                            break;
                    }
                }

                _context.Transactions.Remove(t);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Transaction deleted successfully." });
            }
            catch
            {
                return Json(new { success = false, message = "An error occurred while deleting the transaction." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTransactionCategory(int id, int categoryId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                var transaction = await _context.Transactions
                    .Include(t => t.Category)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null || (!isAdmin && transaction.UserId != userId))
                    return Json(new { success = false, message = "Transaction not found." });

                var category = await _context.Categories.FindAsync(categoryId);
                if (category == null)
                    return Json(new { success = false, message = "Invalid Category." });

                transaction.CategoryId = categoryId;
                await _context.SaveChangesAsync();

                return Json(new { success = true, categoryName = category.Name, message = "Category updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating category: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStatement(IFormFile statementFile, int accountId, string? password)
        {
            if (statementFile == null || statementFile.Length == 0)
            {
                return Json(new { success = false, message = "No file selected or file is empty." });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                using (var stream = statementFile.OpenReadStream())
                {
                    var (importedCount, error) = await _importService.ImportStatementAsync(stream, userId, accountId, password);
                    
                    if (error != null)
                    {
                        return Json(new { success = false, message = error });
                    }

                    return Json(new { success = true, message = $"Successfully imported {importedCount} transactions." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred during import: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSalaryAmount(int id, string pin)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var configPin = _configuration["Security:SalaryPin"] ?? "1234";
            if (pin != configPin)
            {
                return Json(new { success = false, message = "Invalid PIN" });
            }

            var t = await _context.Transactions
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id && (isAdmin || x.UserId == userId));

            if (t == null) return NotFound();

            return Json(new { success = true, amount = t.Amount.ToString("C") });
        }

        private async Task<IEnumerable<SelectListItem>> GetAccountSourcesAsync(string userId, bool isAdmin)
        {
            var sources = await _context.AccountSources
                .Where(a => isAdmin || a.UserId == userId)
                .OrderBy(a => a.Type)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var selectListItems = new List<SelectListItem>();
            var groupedSources = sources.GroupBy(a => a.Type.ToString());

            foreach (var group in groupedSources)
            {
                // In ASP.NET Core MVC, SelectListGroup converts automatically to HTML <optgroup>
                var selectListGroup = new SelectListGroup { Name = group.Key };

                foreach (var account in group)
                {
                    string accountText = string.IsNullOrEmpty(account.AccountNumber) 
                        ? account.Name 
                        : $"{account.Name} - ****{account.AccountNumber[^4..].PadLeft(4, '*').Trim('*').PadLeft(account.AccountNumber.Length > 4 ? 4 : account.AccountNumber.Length, '*')}";

                    selectListItems.Add(new SelectListItem
                    {
                        Value = account.Id.ToString(),
                        Text = accountText,
                        Group = selectListGroup
                    });
                }
            }

            return selectListItems;
        }

        private async Task<IEnumerable<SelectListItem>> GetGroupedCategoriesAsync()
        {
            // Fetch all categories including their parent
            var allCategories = await _context.Categories
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentCategory != null ? c.ParentCategory.Name : c.Name)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var selectListItems = new List<SelectListItem>();
            var groupedCategories = allCategories.GroupBy(c => c.ParentCategory?.Name ?? "Main Categories");

            foreach (var group in groupedCategories)
            {
                var selectListGroup = new SelectListGroup { Name = group.Key };

                foreach (var category in group)
                {
                    selectListItems.Add(new SelectListItem
                    {
                        Value = category.Id.ToString(),
                        Text = category.Name,
                        Group = selectListGroup
                    });
                }
            }

            return selectListItems;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesList()
        {
            var categories = await _context.Categories
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentCategory != null ? c.ParentCategory.Name : "")
                .ThenBy(c => c.Name)
                .Select(c => new {
                    id = c.Id,
                    name = c.Name,
                    parentCategoryId = c.ParentCategoryId,
                    parentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null
                })
                .ToListAsync();
            return Json(new { success = true, categories });
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupedCategoriesJson()
        {
            var selectListItems = await GetGroupedCategoriesAsync();
            var result = selectListItems.Select(i => new {
                value = i.Value,
                text = i.Text,
                group = i.Group?.Name
            });
            return Json(new { success = true, categories = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string name, int? parentCategoryId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Category name is required." });

            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower() && c.ParentCategoryId == parentCategoryId);
            if (exists)
                return Json(new { success = false, message = "Category already exists." });

            var category = new Category { Name = name.Trim(), ParentCategoryId = parentCategoryId };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Category added successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, string name, int? parentCategoryId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Category name is required." });

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return Json(new { success = false, message = "Category not found." });

            var exists = await _context.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower() && c.ParentCategoryId == parentCategoryId);
            if (exists)
                return Json(new { success = false, message = "Another category with this name already exists." });

            // Prevent circular reference
            if (parentCategoryId == id)
                return Json(new { success = false, message = "Category cannot be its own parent." });

            category.Name = name.Trim();
            category.ParentCategoryId = parentCategoryId;
            
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Category updated successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.Include(c => c.SubCategories).Include(c => c.Transactions).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return Json(new { success = false, message = "Category not found." });

            if (category.SubCategories.Any() || category.Transactions.Any())
                return Json(new { success = false, message = "Cannot delete category because it has subcategories or associated transactions." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Category deleted successfully." });
        }
    }
}
