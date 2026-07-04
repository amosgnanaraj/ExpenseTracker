using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Services
{
    public interface IInvestmentImportService
    {
        Task<(int updatedCount, List<string> skippedStocks, string? error)> ImportStocksAsync(Stream fileStream, string userId);
        Task<(int updatedCount, List<string> skippedFunds, string? error)> ImportMutualFundsAsync(Stream fileStream, string userId);
    }

    public class InvestmentImportService : IInvestmentImportService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly ILogger<InvestmentImportService> _logger;

        public InvestmentImportService(ExpenseTrackerDbContext context, ILogger<InvestmentImportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(int updatedCount, List<string> skippedStocks, string? error)> ImportStocksAsync(Stream fileStream, string userId)
        {
            int updatedCount = 0;
            var skippedStocks = new List<string>();

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                    return (0, new List<string>(), "No worksheet found in the Excel file.");

                var headerRow = worksheet.Row(11);
                if (headerRow.IsEmpty())
                    return (0, new List<string>(), "Header row (Row 11) is empty or not found.");

                int nameCol = GetColumnIndex(headerRow, "Stock Name");
                int qtyCol = GetColumnIndex(headerRow, "Quantity");
                int buyPriceCol = GetColumnIndex(headerRow, "Average buy price");
                int currentPriceCol = GetColumnIndex(headerRow, "Closing price");

                if (nameCol == -1 || qtyCol == -1 || buyPriceCol == -1 || currentPriceCol == -1)
                {
                    return (0, new List<string>(), "Required columns not found. Ensure 'Stock Name', 'Quantity', 'Average buy price', and 'Closing price' exist in row 11.");
                }

                var userStocks = await _context.Stocks
                    .Where(s => s.UserId == userId)
                    .ToListAsync();

                var rows = worksheet.RowsUsed().Where(r => r.RowNumber() >= 12);

                foreach (var row in rows)
                {
                    string stockName = row.Cell(nameCol).GetValue<string>().Trim();
                    if (string.IsNullOrEmpty(stockName)) continue;

                    var existingStock = userStocks.FirstOrDefault(s => s.Name.Equals(stockName, StringComparison.OrdinalIgnoreCase));

                    if (existingStock != null)
                    {
                        try
                        {
                            existingStock.Quantity = row.Cell(qtyCol).GetValue<decimal>();
                            existingStock.BuyPrice = row.Cell(buyPriceCol).GetValue<decimal>();
                            existingStock.CurrentPrice = row.Cell(currentPriceCol).GetValue<decimal>();

                            _context.Update(existingStock);
                            updatedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse data for stock: {StockName} at row {RowNumber}", stockName, row.RowNumber());
                            skippedStocks.Add(stockName);
                        }
                    }
                    else
                    {
                        skippedStocks.Add(stockName);
                    }
                }

                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                return (updatedCount, skippedStocks, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing stocks from Excel");
                return (0, new List<string>(), $"Fatal error during import: {ex.Message}");
            }
        }

        public async Task<(int updatedCount, List<string> skippedFunds, string? error)> ImportMutualFundsAsync(Stream fileStream, string userId)
        {
            int updatedCount = 0;
            var skippedFunds = new List<string>();

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                    return (0, new List<string>(), "No worksheet found in the Excel file.");

                // Header is in row 21
                var headerRow = worksheet.Row(21);
                if (headerRow.IsEmpty())
                    return (0, new List<string>(), "Header row (Row 21) is empty or not found.");

                int folioCol = GetColumnIndex(headerRow, "Folio No.");
                int unitsCol = GetColumnIndex(headerRow, "Units");
                int investedCol = GetColumnIndex(headerRow, "Invested Value");
                int currentValCol = GetColumnIndex(headerRow, "Current Value");
                int nameCol = GetColumnIndex(headerRow, "Scheme Name");

                if (folioCol == -1 || unitsCol == -1 || investedCol == -1 || currentValCol == -1 || nameCol == -1)
                {
                    return (0, new List<string>(), "Required columns not found. Ensure 'Folio No.', 'Units', 'Invested Value', 'Current Value', and 'Scheme Name' exist in row 21.");
                }

                var userFunds = await _context.MutualFunds
                    .Where(m => m.UserId == userId)
                    .ToListAsync();

                // Data starts from row 23
                var rows = worksheet.RowsUsed().Where(r => r.RowNumber() >= 23);

                foreach (var row in rows)
                {
                    string folio = row.Cell(folioCol).GetValue<string>().Trim();
                    string schemeName = row.Cell(nameCol).GetValue<string>().Trim();
                    
                    if (string.IsNullOrEmpty(folio) || string.IsNullOrEmpty(schemeName)) continue;

                    var existingFund = userFunds.FirstOrDefault(m => 
                        m.FolioNumber == folio && 
                        m.Name.Equals(schemeName, StringComparison.OrdinalIgnoreCase));

                    if (existingFund != null)
                    {
                        try
                        {
                            decimal units = row.Cell(unitsCol).GetValue<decimal>();
                            decimal investedValue = row.Cell(investedCol).GetValue<decimal>();
                            decimal currentValue = row.Cell(currentValCol).GetValue<decimal>();

                            if (units != 0)
                            {
                                existingFund.Units = units;
                                existingFund.AvgNAV = investedValue / units;
                                existingFund.CurrentNAV = currentValue / units;

                                _context.Update(existingFund);
                                updatedCount++;
                            }
                            else
                            {
                                skippedFunds.Add($"{schemeName} (Folio: {folio}) - Zero units");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse data for MF: {Folio} at row {RowNumber}", folio, row.RowNumber());
                            skippedFunds.Add($"{schemeName} (Folio: {folio})");
                        }
                    }
                    else
                    {
                        skippedFunds.Add($"{schemeName} (Folio: {folio})");
                    }
                }

                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                return (updatedCount, skippedFunds, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing mutual funds from Excel");
                return (0, new List<string>(), $"Fatal error during import: {ex.Message}");
            }
        }

        private int GetColumnIndex(IXLRow headerRow, string columnName)
        {
            foreach (var cell in headerRow.CellsUsed())
            {
                if (cell.GetValue<string>().Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return cell.Address.ColumnNumber;
                }
            }
            return -1;
        }
    }
}
