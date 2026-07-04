using System.Collections.Generic;

namespace ExpenseTracker.Models
{
    public class InvestmentsViewModel
    {
        public List<Stock> Stocks { get; set; } = new();
        public List<MutualFund> MutualFunds { get; set; } = new();
        public List<FixedDeposit> FixedDeposits { get; set; } = new();
        public List<NPS> NPSs { get; set; } = new();
        public List<EPF> EPFs { get; set; } = new();

        public decimal TotalStockValue { get; set; }
        public decimal TotalMFValue { get; set; }
        public decimal TotalFDValue { get; set; }
        public decimal TotalNPSValue { get; set; }
        public decimal TotalEPFValue { get; set; }
        
        public decimal TotalStockInvested { get; set; }
        public decimal TotalMFInvested { get; set; }
        public decimal TotalNPSInvested { get; set; }
        public decimal TotalFDInvested { get; set; }
        public decimal TotalEPFInvested { get; set; }

        public decimal GrandTotalValue => TotalStockValue + TotalMFValue + TotalFDValue + TotalNPSValue + TotalEPFValue;
        public decimal GrandTotalInvested => TotalStockInvested + TotalMFInvested + TotalNPSInvested + TotalFDInvested + TotalEPFInvested;
        
        public decimal TotalStockPL { get; set; }
        public decimal TotalMFPL { get; set; }
        public decimal TotalNPSPL { get; set; }
        public decimal TotalFDPL { get; set; }
        public decimal TotalEPFPL { get; set; }
        public decimal GrandTotalPL => TotalStockPL + TotalMFPL + TotalNPSPL + TotalFDPL + TotalEPFPL;
        public decimal GrandTotalPLPercentage => GrandTotalInvested > 0 ? (GrandTotalPL / GrandTotalInvested) * 100 : 0;

        // Sorting state
        public string? CurrentSort { get; set; }
        public string? ActiveTab { get; set; }
    }
}
