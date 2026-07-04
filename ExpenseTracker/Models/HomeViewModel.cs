using System.Collections.Generic;

namespace ExpenseTracker.Models
{
    public class HomeViewModel
    {
        // Account Totals
        public decimal TotalBankBalance { get; set; }
        public decimal TotalCreditCardDebt { get; set; }
        public decimal TotalLoanDebt { get; set; }
        public decimal TotalInvestmentValue { get; set; }
        public decimal TotalAssets => TotalBankBalance + TotalInvestmentValue;
        public IEnumerable<AccountSource> Accounts { get; set; } = new List<AccountSource>();
        public IEnumerable<AccountSource> DebtAccounts => Accounts.Where(a => a.Type == AccountType.Loan || a.Type == AccountType.CreditCard);

        // Monthly Analysis Period
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }

        // Monthly Summary
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpense { get; set; }

        // Chart Data: Category Breakdown
        public List<string> CategoryLabels { get; set; } = new List<string>();
        public List<decimal> CategoryValues { get; set; } = new List<decimal>();
        public List<string> OtherCategoryLabels { get; set; } = new List<string>();
        public List<decimal> OtherCategoryValues { get; set; } = new List<decimal>();

        // Chart Data: Daily Spending Trend
        public List<string> DailyLabels { get; set; } = new List<string>();
        public List<decimal> DailyValues { get; set; } = new List<decimal>();

        // Chart Data: 6-Month Trend
        public List<string> TrendLabels { get; set; } = new List<string>();
        public List<decimal> TrendIncomeValues { get; set; } = new List<decimal>();
        public List<decimal> TrendExpenseValues { get; set; } = new List<decimal>();

        // NEW: Investment Insights
        public List<string> InvestmentTypeLabels { get; set; } = new List<string>();
        public List<decimal> InvestmentTypeValues { get; set; } = new List<decimal>();

        public List<string> PerformanceLabels { get; set; } = new List<string>() { "Total Cost", "Current Value" };
        public List<decimal> PerformanceValues { get; set; } = new List<decimal>();

        public List<TopHoldingViewModel> TopHoldings { get; set; } = new List<TopHoldingViewModel>();

        // Financial Wealth Insights
        public decimal SavingsRate { get; set; }
        public decimal SpendingVelocity { get; set; }
        public decimal FinancialRunway { get; set; }
        public decimal AvgMonthlyExpense { get; set; }

        // 50/30/20 Budgeting Insights
        public decimal BudgetNeedsAmount { get; set; }
        public decimal BudgetWantsAmount { get; set; }
        public decimal BudgetSavingsAmount { get; set; }

        public decimal BudgetNeedsPercentage { get; set; }
        public decimal BudgetWantsPercentage { get; set; }
        public decimal BudgetSavingsPercentage { get; set; }

        // Active Investment additions for the month
        public decimal ActiveInvestmentsAmount { get; set; }

        // Chart Data: 6-Month Net Worth Trend
        public List<string> NetWorthLabels { get; set; } = new List<string>();
        public List<decimal> NetWorthValues { get; set; } = new List<decimal>();
        public decimal CurrentNetWorth => TotalBankBalance + TotalInvestmentValue - TotalCreditCardDebt - TotalLoanDebt;

        // Credit Health Metrics
        public decimal DtiRatio { get; set; }
        public decimal DtaRatio { get; set; }
        public decimal MonthlyDebtRepayments { get; set; }
        public decimal TotalDebt => TotalCreditCardDebt + TotalLoanDebt;
    }

    public class TopHoldingViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal ProfitLossPercentage { get; set; }
    }
}
