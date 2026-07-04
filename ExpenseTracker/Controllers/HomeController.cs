using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ExpenseTrackerDbContext _context;

    public HomeController(ILogger<HomeController> logger, ExpenseTrackerDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index(int? month, int? year)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Set selected period (default to current month/year)
        int selectedMonth = month ?? DateTime.Now.Month;
        int selectedYear = year ?? DateTime.Now.Year;

        var accounts = await _context.AccountSources
            .Where(a => isAdmin || a.UserId == userId)
            .ToListAsync();

        // Start of selected month (local time)
        var startDate = new DateTime(selectedYear, selectedMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        // Fetch transactions for the selected month (taking SalaryMonth/SalaryYear override into account)
        var transactions = await _context.Transactions
            .Include(t => t.Category)
                .ThenInclude(c => c.ParentCategory)
            .Include(t => t.AccountSource)
            .Include(t => t.TransferToAccountSource)
            .Where(t => isAdmin || t.UserId == userId)
            .Where(t =>
                (t.SalaryMonth == selectedMonth && t.SalaryYear == selectedYear) ||
                (
                    (t.SalaryMonth == null || t.SalaryYear == null) &&
                    t.Date >= startDate && t.Date < endDate
                )
            )
            .ToListAsync();

        // Helper to check if a transaction should be treated as an expense
        // 1. All Debits are expenses.
        // 2. Transfers to Loan or Credit Card accounts are expenses (Debt repayment/EMI).
        bool IsExpense(Transaction t) => t.Type == TransactionType.Debit || 
            (t.Type == TransactionType.Transfer && t.TransferToAccountSource != null && 
             (t.TransferToAccountSource.Type == AccountType.Loan || t.TransferToAccountSource.Type == AccountType.CreditCard));

        // Calculate Monthly Summary
        var monthlyIncome = transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        var monthlyExpense = transactions.Where(IsExpense).Sum(t => t.Amount);

        // Category Breakdown (Expenses only)
        var fullCategoryGroup = transactions
            .Where(IsExpense)
            .GroupBy(t => t.Category?.Name ?? "Uncategorized")
            .Select(g => new { Label = g.Key, Value = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Value)
            .ToList();

        var categoryLabels = new List<string>();
        var categoryValues = new List<decimal>();
        var otherCategoryLabels = new List<string>();
        var otherCategoryValues = new List<decimal>();

        if (fullCategoryGroup.Count > 6)
        {
            var top6 = fullCategoryGroup.Take(6).ToList();
            var others = fullCategoryGroup.Skip(6).ToList();
            
            categoryLabels = top6.Select(x => x.Label).Concat(new[] { "Other" }).ToList();
            categoryValues = top6.Select(x => x.Value).Concat(new[] { others.Sum(x => x.Value) }).ToList();
            
            otherCategoryLabels = others.Select(x => x.Label).ToList();
            otherCategoryValues = others.Select(x => x.Value).ToList();
        }
        else
        {
            categoryLabels = fullCategoryGroup.Select(x => x.Label).ToList();
            categoryValues = fullCategoryGroup.Select(x => x.Value).ToList();
        }

        // Daily Spending Trend (Expenses only)
        var dailyGroup = transactions
            .Where(IsExpense)
            .GroupBy(t => t.Date.Day)
            .Select(g => new { Day = g.Key, Value = g.Sum(t => t.Amount) })
            .OrderBy(x => x.Day)
            .ToList();

        // 6-Month Trend Logic
        var trendStartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        int trendStartValue = trendStartDate.Year * 12 + trendStartDate.Month;
        var allTrendTransactions = await _context.Transactions
            .Include(t => t.TransferToAccountSource)
            .Where(t => isAdmin || t.UserId == userId)
            .Where(t =>
                (t.SalaryMonth != null && t.SalaryYear != null && (t.SalaryYear * 12 + t.SalaryMonth) >= trendStartValue) ||
                ((t.SalaryMonth == null || t.SalaryYear == null) && t.Date >= trendStartDate)
            )
            .ToListAsync();

        var trendData = new List<dynamic>();
        for (int i = 0; i < 6; i++)
        {
            var targetMonth = trendStartDate.AddMonths(i);
            var monthTransactions = allTrendTransactions
                .Where(t =>
                    (t.SalaryMonth == targetMonth.Month && t.SalaryYear == targetMonth.Year) ||
                    ((t.SalaryMonth == null || t.SalaryYear == null) && t.Date.Year == targetMonth.Year && t.Date.Month == targetMonth.Month)
                )
                .ToList();

            trendData.Add(new
            {
                Label = targetMonth.ToString("MMM yy"),
                Income = monthTransactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount),
                Expense = monthTransactions.Where(IsExpense).Sum(t => t.Amount)
            });
        }

        // Calculate Investment Totals
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();
            var mfs = await _context.MutualFunds.Where(m => isAdmin || m.UserId == userId).ToListAsync();
            var fds = await _context.FixedDeposits.Where(f => isAdmin || f.UserId == userId).ToListAsync();
            var npss = await _context.NPSs.Where(n => isAdmin || n.UserId == userId).ToListAsync();
            var epfs = await _context.EPFs.Where(e => isAdmin || e.UserId == userId).ToListAsync();

            ViewBag.TotalInvestmentValue = stocks.Sum(s => s.CurrentValue) + 
                                         mfs.Sum(m => m.CurrentValue) + 
                                         fds.Sum(f => f.CurrentValue) +
                                         npss.Sum(n => n.CurrentValue) +
                                         epfs.Sum(e => e.CurrentValue);

        // --- 6-MONTH HISTORICAL NET WORTH BACKTRACKING ---
        var netWorthLabels = new List<string>();
        var netWorthValues = new List<decimal>();

        for (int i = 0; i < 6; i++)
        {
            var targetMonth = trendStartDate.AddMonths(i);
            var monthEnd = new DateTime(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

            // Reconstruct the balance of each account at monthEnd
            decimal totalBankBalanceM = 0;
            decimal totalCCDebtM = 0;
            decimal totalLoanDebtM = 0;

            foreach (var account in accounts)
            {
                decimal balanceM = account.Balance;

                // Find all transactions for this account that happened after monthEnd
                var postTransactions = allTrendTransactions
                    .Where(t => t.Date >= monthEnd)
                    .ToList();

                foreach (var t in postTransactions)
                {
                    // If the transaction was debited from this account:
                    if (t.AccountSourceId == account.Id)
                    {
                        if (t.Type == TransactionType.Debit || t.Type == TransactionType.Transfer)
                        {
                            balanceM += t.Amount; // Add back the withdrawn/transferred amount
                        }
                        else if (t.Type == TransactionType.Credit)
                        {
                            balanceM -= t.Amount; // Subtract the credited/deposited amount
                        }
                    }
                    // If the transaction was a Transfer into this account:
                    else if (t.TransferToAccountSourceId == account.Id && t.Type == TransactionType.Transfer)
                    {
                        balanceM -= t.Amount; // Subtract the transferred-in amount
                    }
                }

                if (account.Type == AccountType.Bank)
                {
                    totalBankBalanceM += balanceM;
                }
                else if (account.Type == AccountType.CreditCard)
                {
                    totalCCDebtM += balanceM;
                }
                else if (account.Type == AccountType.Loan)
                {
                    totalLoanDebtM += balanceM;
                }
            }

            // Reconstruct investment values active at monthEnd (PurchaseDate < monthEnd)
            decimal totalInvestmentValueM = 0;
            
            decimal stocksM = stocks.Where(s => s.PurchaseDate < monthEnd).Sum(s => s.CurrentValue);
            decimal mfsM = mfs.Where(m => m.PurchaseDate < monthEnd).Sum(m => m.CurrentValue);
            decimal fdsM = fds.Where(f => f.PurchaseDate < monthEnd).Sum(f => f.CurrentValue);
            decimal npssM = npss.Where(n => n.PurchaseDate < monthEnd).Sum(n => n.CurrentValue);
            decimal epfsM = epfs.Where(e => e.PurchaseDate < monthEnd).Sum(e => e.CurrentValue);
            
            totalInvestmentValueM = stocksM + mfsM + fdsM + npssM + epfsM;

            decimal netWorthM = totalBankBalanceM + totalInvestmentValueM - totalCCDebtM - totalLoanDebtM;

            netWorthLabels.Add(targetMonth.ToString("MMM yy"));
            netWorthValues.Add(netWorthM);
        }

        // --- FINANCIAL INSIGHTS CALCULATIONS ---
        
        // 1. Savings Rate
        decimal savingsRate = 0;
        if (monthlyIncome > 0)
        {
            savingsRate = ((monthlyIncome - monthlyExpense) / monthlyIncome) * 100;
        }

        // 2. Spending Velocity (MTD comparison)
        int currentDay = DateTime.UtcNow.Day;
        var currentMTD = transactions.Where(t => IsExpense(t) && t.Date.Day <= currentDay).Sum(t => t.Amount);
        
        var prevMonthStart = startDate.AddMonths(-1);
        var prevMonthEnd = prevMonthStart.AddMonths(1);
        var prevMonthTransactions = await _context.Transactions
            .Include(t => t.TransferToAccountSource)
            .Where(t => (isAdmin || t.UserId == userId) && t.Date >= prevMonthStart && t.Date < prevMonthEnd)
            .ToListAsync();
            
        var prevMTD = prevMonthTransactions.Where(t => IsExpense(t) && t.Date.Day <= currentDay).Sum(t => t.Amount);
        decimal spendingVelocity = 0;
        if (prevMTD > 0)
        {
            spendingVelocity = ((currentMTD - prevMTD) / prevMTD) * 100;
        }

        // 3. Financial Runway
        var totalExpensesLast6Months = trendData.Sum(x => (decimal)x.Expense);
        decimal avgMonthlyExpense = trendData.Count > 0 ? totalExpensesLast6Months / trendData.Count : 0;
        decimal financialRunway = 0;
        if (avgMonthlyExpense > 0)
        {
            financialRunway = (accounts.Where(a => a.Type == AccountType.Bank).Sum(a => a.Balance)) / avgMonthlyExpense;
        }

        // 4. Credit & Debt Health Analysis
        decimal monthlyDebtRepayments = transactions
            .Where(t => t.Type == TransactionType.Transfer && t.TransferToAccountSource != null && 
                (t.TransferToAccountSource.Type == AccountType.Loan || t.TransferToAccountSource.Type == AccountType.CreditCard))
            .Sum(t => t.Amount);

        decimal dtiRatio = 0;
        if (monthlyIncome > 0)
        {
            dtiRatio = (monthlyDebtRepayments / monthlyIncome) * 100;
        }
        else if (monthlyDebtRepayments > 0)
        {
            dtiRatio = 100;
        }

        decimal dtaRatio = 0;
        decimal totalAssetsValue = accounts.Where(a => a.Type == AccountType.Bank).Sum(a => a.Balance) + (decimal)ViewBag.TotalInvestmentValue;
        decimal totalDebtValue = accounts.Where(a => a.Type == AccountType.CreditCard).Sum(a => a.Balance) + accounts.Where(a => a.Type == AccountType.Loan).Sum(a => a.Balance);
        
        if (totalAssetsValue > 0)
        {
            dtaRatio = (totalDebtValue / totalAssetsValue) * 100;
        }
        else if (totalDebtValue > 0)
        {
            dtaRatio = 100;
        }

        // --- 50/30/20 BUDGETING RULE CALCULATIONS ---
        
        // Define list of Need keywords based on category names
        var needKeywords = new[] { "utility", "utilities", "electricity", "internet", "water", "gas", "groceries", "healthcare", "medical", "rent", "insurance", "emi", "loan", "bills", "transport", "fuel", "public transit" };
        
        // Helper to check if a category is a "Need"
        bool IsNeedCategory(Category? cat)
        {
            if (cat == null) return false;
            var name = cat.Name.ToLowerInvariant();
            
            // Check direct name
            if (needKeywords.Any(k => name.Contains(k))) return true;
            
            // Check parent category name
            if (cat.ParentCategory != null)
            {
                var parentName = cat.ParentCategory.Name.ToLowerInvariant();
                if (needKeywords.Any(k => parentName.Contains(k))) return true;
            }
            
            return false;
        }

        // Categorize expenses in the selected month
        decimal budgetNeedsAmount = 0;
        decimal budgetWantsAmount = 0;
        
        var monthlyExpensesList = transactions.Where(IsExpense).ToList();
        
        foreach (var t in monthlyExpensesList)
        {
            // Transfers to Loan/CreditCard are debt repayments, thus "Needs"
            if (t.Type == TransactionType.Transfer && t.TransferToAccountSource != null && 
                (t.TransferToAccountSource.Type == AccountType.Loan || t.TransferToAccountSource.Type == AccountType.CreditCard))
            {
                budgetNeedsAmount += t.Amount;
            }
            // Check based on category name
            else if (IsNeedCategory(t.Category))
            {
                budgetNeedsAmount += t.Amount;
            }
            // Otherwise, it's a discretionary "Want"
            else
            {
                budgetWantsAmount += t.Amount;
            }
        }

        // Savings & Investments is the remaining portion of the income
        decimal budgetSavingsAmount = 0;
        if (monthlyIncome > 0)
        {
            budgetSavingsAmount = Math.Max(0, monthlyIncome - budgetNeedsAmount - budgetWantsAmount);
        }

        // Calculate active investments added during this month
        var monthStartDate = new DateTime(selectedYear, selectedMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndDate = monthStartDate.AddMonths(1);

        decimal activeInvestmentsAmount = 0;
        
        activeInvestmentsAmount += fds
            .Where(f => f.PurchaseDate >= monthStartDate && f.PurchaseDate < monthEndDate)
            .Sum(f => f.PrincipalAmount);
            
        activeInvestmentsAmount += npss
            .Where(n => n.PurchaseDate >= monthStartDate && n.PurchaseDate < monthEndDate)
            .Sum(n => n.TotalInvested);
            
        activeInvestmentsAmount += stocks
            .Where(s => s.PurchaseDate >= monthStartDate && s.PurchaseDate < monthEndDate)
            .Sum(s => s.TotalCost);
            
        activeInvestmentsAmount += mfs
            .Where(m => m.PurchaseDate >= monthStartDate && m.PurchaseDate < monthEndDate)
            .Sum(m => m.TotalCost);

        activeInvestmentsAmount += epfs
            .Where(e => e.PurchaseDate >= monthStartDate && e.PurchaseDate < monthEndDate)
            .Sum(e => e.EmployeeContribution);

        // Compute percentages relative to total income (if income > 0)
        decimal budgetNeedsPercentage = 0;
        decimal budgetWantsPercentage = 0;
        decimal budgetSavingsPercentage = 0;

        decimal totalBudgetBase = monthlyIncome > 0 ? monthlyIncome : (budgetNeedsAmount + budgetWantsAmount);
        if (totalBudgetBase > 0)
        {
            budgetNeedsPercentage = (budgetNeedsAmount / totalBudgetBase) * 100;
            budgetWantsPercentage = (budgetWantsAmount / totalBudgetBase) * 100;
            budgetSavingsPercentage = monthlyIncome > 0 
                ? (budgetSavingsAmount / totalBudgetBase) * 100 
                : 0;
        }

        var viewModel = new HomeViewModel
        {
            SavingsRate = savingsRate,
            SpendingVelocity = spendingVelocity,
            FinancialRunway = financialRunway,
            AvgMonthlyExpense = avgMonthlyExpense,
            DtiRatio = dtiRatio,
            DtaRatio = dtaRatio,
            MonthlyDebtRepayments = monthlyDebtRepayments,
            Accounts = accounts,
            TotalBankBalance = accounts.Where(a => a.Type == AccountType.Bank).Sum(a => a.Balance),
            TotalCreditCardDebt = accounts.Where(a => a.Type == AccountType.CreditCard).Sum(a => a.Balance),
            TotalLoanDebt = accounts.Where(a => a.Type == AccountType.Loan).Sum(a => a.Balance),
            TotalInvestmentValue = ViewBag.TotalInvestmentValue,

            BudgetNeedsAmount = budgetNeedsAmount,
            BudgetWantsAmount = budgetWantsAmount,
            BudgetSavingsAmount = budgetSavingsAmount,
            BudgetNeedsPercentage = budgetNeedsPercentage,
            BudgetWantsPercentage = budgetWantsPercentage,
            BudgetSavingsPercentage = budgetSavingsPercentage,
            ActiveInvestmentsAmount = activeInvestmentsAmount,

            NetWorthLabels = netWorthLabels,
            NetWorthValues = netWorthValues,
 
            SelectedMonth = selectedMonth,
            SelectedYear = selectedYear,
            MonthlyIncome = monthlyIncome,
            MonthlyExpense = monthlyExpense,

            CategoryLabels = categoryLabels,
            CategoryValues = categoryValues,
            OtherCategoryLabels = otherCategoryLabels,
            OtherCategoryValues = otherCategoryValues,

            DailyLabels = dailyGroup.Select(x => x.Day.ToString()).ToList(),
            DailyValues = dailyGroup.Select(x => x.Value).ToList(),

            TrendLabels = trendData.Select(x => (string)x.Label).ToList(),
            TrendIncomeValues = trendData.Select(x => (decimal)x.Income).ToList(),
            TrendExpenseValues = trendData.Select(x => (decimal)x.Expense).ToList(),

            // Investment Insights
            InvestmentTypeLabels = new List<string> { "Stocks", "Mutual Funds", "FDs", "NPS", "EPF" },
            InvestmentTypeValues = new List<decimal> { 
                stocks.Sum(s => s.CurrentValue), 
                mfs.Sum(m => m.CurrentValue), 
                fds.Sum(f => f.CurrentValue),
                npss.Sum(n => n.CurrentValue),
                epfs.Sum(e => e.CurrentValue)
            },

            PerformanceValues = new List<decimal> {
                stocks.Sum(s => s.Quantity * s.BuyPrice) + mfs.Sum(m => m.Units * m.AvgNAV) + fds.Sum(f => f.PrincipalAmount) + npss.Sum(n => n.TotalInvested) + epfs.Sum(e => e.EmployeeContribution),
                stocks.Sum(s => s.CurrentValue) + mfs.Sum(m => m.CurrentValue) + fds.Sum(f => f.CurrentValue) + npss.Sum(n => n.CurrentValue) + epfs.Sum(e => e.CurrentValue)
            },

            TopHoldings = stocks.Select(s => new TopHoldingViewModel { 
                Name = s.Name, Type = "Stock", CurrentValue = s.CurrentValue, ProfitLoss = s.ProfitLoss, ProfitLossPercentage = s.BuyPrice > 0 ? (s.CurrentPrice - s.BuyPrice) / s.BuyPrice * 100 : 0 
            })
            .Concat(mfs.Select(m => new TopHoldingViewModel { 
                Name = m.Name, Type = "Mutual Fund", CurrentValue = m.CurrentValue, ProfitLoss = m.ProfitLoss, ProfitLossPercentage = m.AvgNAV > 0 ? (m.CurrentNAV - m.AvgNAV) / m.AvgNAV * 100 : 0 
            }))
            .Concat(npss.Select(n => new TopHoldingViewModel {
                Name = n.SchemeName, Type = "NPS", CurrentValue = n.CurrentValue, ProfitLoss = 0, ProfitLossPercentage = 0
            }))
            .Concat(epfs.Select(e => new TopHoldingViewModel {
                Name = "EPF (" + e.UAN + ")", Type = "EPF", CurrentValue = e.CurrentValue, ProfitLoss = e.ProfitLoss, ProfitLossPercentage = e.EmployeeContribution > 0 ? (e.ProfitLoss / e.EmployeeContribution) * 100 : 0
            }))
            .OrderByDescending(x => x.CurrentValue)
            .Take(5)
            .ToList()
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
