using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class InvestmentsController : Controller
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly Services.IStockPriceService _stockPriceService;
        private readonly Services.IInvestmentImportService _investmentImportService;
        private readonly Services.ITechnicalAnalysisService _technicalAnalysisService;
        private readonly Services.IFundamentalAnalysisService _fundamentalAnalysisService;

        public InvestmentsController(ExpenseTrackerDbContext context, Services.IStockPriceService stockPriceService, Services.IInvestmentImportService investmentImportService, Services.ITechnicalAnalysisService technicalAnalysisService, Services.IFundamentalAnalysisService fundamentalAnalysisService)
        {
            _context = context;
            _stockPriceService = stockPriceService;
            _investmentImportService = investmentImportService;
            _technicalAnalysisService = technicalAnalysisService;
            _fundamentalAnalysisService = fundamentalAnalysisService;
        }

        // GET: Investments
        public async Task<IActionResult> Index(string sortOrder, string activeTab)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var viewModel = new InvestmentsViewModel
            {
                CurrentSort = sortOrder,
                ActiveTab = activeTab ?? "Stocks"
            };

            // Fetch from separate tables
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();
            var mfs = await _context.MutualFunds.Where(m => isAdmin || m.UserId == userId).ToListAsync();
            var fds = await _context.FixedDeposits.Where(f => isAdmin || f.UserId == userId).ToListAsync();
            var npss = await _context.NPSs.Where(n => isAdmin || n.UserId == userId).ToListAsync();
            var epfs = await _context.EPFs.Where(e => isAdmin || e.UserId == userId).ToListAsync();

            // Set sort params for the view (maintaining existing logic)
            ViewBag.NameSortParm = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.PriceSortParm = sortOrder == "Price" ? "price_desc" : "Price";
            ViewBag.QtySortParm = sortOrder == "Qty" ? "qty_desc" : "Qty";
            ViewBag.CostSortParm = sortOrder == "Cost" ? "cost_desc" : "Cost";
            ViewBag.ValueSortParm = sortOrder == "Value" ? "value_desc" : "Value";
            ViewBag.PLSortParm = sortOrder == "PL" ? "pl_desc" : "PL";

            // Apply sorting to each list
            viewModel.Stocks = ApplyStockSort(stocks, sortOrder);
            viewModel.MutualFunds = ApplyMFSort(mfs, sortOrder);
            viewModel.FixedDeposits = ApplyFDSort(fds, sortOrder);
            viewModel.NPSs = ApplyNPSSort(npss, sortOrder);
            viewModel.EPFs = ApplyEPFSort(epfs, sortOrder);

            // Calculate Totals
            viewModel.TotalStockValue = viewModel.Stocks.Sum(s => s.CurrentValue);
            viewModel.TotalMFValue = viewModel.MutualFunds.Sum(m => m.CurrentValue);
            viewModel.TotalFDValue = viewModel.FixedDeposits.Sum(f => f.CurrentValue);
            viewModel.TotalNPSValue = viewModel.NPSs.Sum(n => n.CurrentValue);
            viewModel.TotalEPFValue = viewModel.EPFs.Sum(e => e.CurrentValue);

            viewModel.TotalStockInvested = viewModel.Stocks.Sum(s => s.TotalCost);
            viewModel.TotalMFInvested = viewModel.MutualFunds.Sum(m => m.TotalCost);
            viewModel.TotalNPSInvested = viewModel.NPSs.Sum(n => n.TotalInvested);
            viewModel.TotalFDInvested = viewModel.FixedDeposits.Sum(f => f.PrincipalAmount);
            viewModel.TotalEPFInvested = viewModel.EPFs.Sum(e => e.EmployeeContribution);

            viewModel.TotalStockPL = viewModel.Stocks.Sum(s => s.ProfitLoss);
            viewModel.TotalMFPL = viewModel.MutualFunds.Sum(m => m.ProfitLoss);
            viewModel.TotalNPSPL = viewModel.NPSs.Sum(n => n.ProfitLoss);
            viewModel.TotalFDPL = viewModel.FixedDeposits.Sum(f => f.ProfitLoss);
            viewModel.TotalEPFPL = viewModel.EPFs.Sum(e => e.ProfitLoss);

            return View(viewModel);
        }

        private List<Stock> ApplyStockSort(List<Stock> list, string sortOrder)
        {
            return sortOrder switch
            {
                "name_desc" => list.OrderByDescending(i => i.Name).ToList(),
                "Price" => list.OrderBy(i => i.BuyPrice).ToList(),
                "price_desc" => list.OrderByDescending(i => i.BuyPrice).ToList(),
                "Qty" => list.OrderBy(i => i.Quantity).ToList(),
                "qty_desc" => list.OrderByDescending(i => i.Quantity).ToList(),
                "Cost" => list.OrderBy(i => i.TotalCost).ToList(),
                "cost_desc" => list.OrderByDescending(i => i.TotalCost).ToList(),
                "Value" => list.OrderBy(i => i.CurrentValue).ToList(),
                "value_desc" => list.OrderByDescending(i => i.CurrentValue).ToList(),
                "PL" => list.OrderBy(i => i.ProfitLoss).ToList(),
                "pl_desc" => list.OrderByDescending(i => i.ProfitLoss).ToList(),
                _ => list.OrderBy(i => i.Name).ToList(),
            };
        }

        private List<MutualFund> ApplyMFSort(List<MutualFund> list, string sortOrder)
        {
            return sortOrder switch
            {
                "name_desc" => list.OrderByDescending(i => i.Name).ToList(),
                "Price" => list.OrderBy(i => i.AvgNAV).ToList(),
                "price_desc" => list.OrderByDescending(i => i.AvgNAV).ToList(),
                "Qty" => list.OrderBy(i => i.Units).ToList(),
                "qty_desc" => list.OrderByDescending(i => i.Units).ToList(),
                "Cost" => list.OrderBy(i => i.TotalCost).ToList(),
                "cost_desc" => list.OrderByDescending(i => i.TotalCost).ToList(),
                "Value" => list.OrderBy(i => i.CurrentValue).ToList(),
                "value_desc" => list.OrderByDescending(i => i.CurrentValue).ToList(),
                "PL" => list.OrderBy(i => i.ProfitLoss).ToList(),
                "pl_desc" => list.OrderByDescending(i => i.ProfitLoss).ToList(),
                _ => list.OrderBy(i => i.Name).ToList(),
            };
        }

        private List<NPS> ApplyNPSSort(List<NPS> list, string sortOrder)
        {
            return sortOrder switch
            {
                "name_desc" => list.OrderByDescending(i => i.SchemeName).ToList(),
                "Price" => list.OrderBy(i => i.CurrentNAV).ToList(),
                "price_desc" => list.OrderByDescending(i => i.CurrentNAV).ToList(),
                "Qty" => list.OrderBy(i => i.TotalUnits).ToList(),
                "qty_desc" => list.OrderByDescending(i => i.TotalUnits).ToList(),
                "Cost" => list.OrderBy(i => i.TotalInvested).ToList(),
                "cost_desc" => list.OrderByDescending(i => i.TotalInvested).ToList(),
                "Value" => list.OrderBy(i => i.CurrentValue).ToList(),
                "value_desc" => list.OrderByDescending(i => i.CurrentValue).ToList(),
                "PL" => list.OrderBy(i => i.ProfitLoss).ToList(),
                "pl_desc" => list.OrderByDescending(i => i.ProfitLoss).ToList(),
                _ => list.OrderBy(i => i.SchemeName).ToList(),
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStocks(Microsoft.AspNetCore.Http.IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid Excel file.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(Index));
            }

            using var stream = file.OpenReadStream();
            var (updated, skippedList, error) = await _investmentImportService.ImportStocksAsync(stream, userId);

            if (error != null)
            {
                TempData["Error"] = error;
            }
            else
            {
                var message = $"Import completed. Updated: {updated}, Skipped: {skippedList.Count}.";
                if (skippedList.Any())
                {
                    message += " The following stocks were not found or failed: " + string.Join(", ", skippedList.Take(10));
                    if (skippedList.Count > 10) message += "...";
                }
                TempData["Success"] = message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportMutualFunds(Microsoft.AspNetCore.Http.IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid Excel file.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(Index));
            }

            using var stream = file.OpenReadStream();
            var (updated, skippedList, error) = await _investmentImportService.ImportMutualFundsAsync(stream, userId);

            if (error != null)
            {
                TempData["Error"] = error;
            }
            else
            {
                var message = $"Import completed. Updated: {updated}, Skipped: {skippedList.Count}.";
                if (skippedList.Any())
                {
                    message += " The following funds were not found or failed: " + string.Join(", ", skippedList.Take(10));
                    if (skippedList.Count > 10) message += "...";
                }
                TempData["Success"] = message;
            }

            return RedirectToAction(nameof(Index));
        }

        private List<FixedDeposit> ApplyFDSort(List<FixedDeposit> list, string sortOrder)
        {
            return sortOrder switch
            {
                "name_desc" => list.OrderByDescending(i => i.BankName).ToList(),
                "Cost" => list.OrderBy(i => i.PrincipalAmount).ToList(),
                "cost_desc" => list.OrderByDescending(i => i.PrincipalAmount).ToList(),
                _ => list.OrderBy(i => i.BankName).ToList(),
            };
        }

        // GET: Investments/Create
        public async Task<IActionResult> Create()
        {
            return View();
        }

        public IActionResult CreateStock() => View();
        public IActionResult CreateMF() => View();
        public IActionResult CreateNPS() => View();
        public IActionResult CreateFD()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            ViewBag.Banks = _context.AccountSources
                .Where(a => (isAdmin || a.UserId == userId) && a.Type == AccountType.Bank)
                .Select(a => a.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            return View();
        }

        // --- STOCKS ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStock(Stock stock)
        {
            stock.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            stock.PurchaseDate = stock.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Add(stock);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { activeTab = "Stocks" });
            }
            return View("Create", stock);
        }

        public async Task<IActionResult> EditStock(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null) return NotFound();
            if (IsAjax()) return PartialView(stock);
            return View(stock);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStock(int id, Stock stock)
        {
            if (id != stock.Id) return NotFound();
            stock.PurchaseDate = stock.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Update(stock);
                await _context.SaveChangesAsync();
                if (IsAjax()) return Json(new { success = true, type = "Stock" });
                return RedirectToAction(nameof(Index), new { activeTab = "Stocks" });
            }
            if (IsAjax()) return PartialView(stock);
            return View(stock);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStock(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock != null)
            {
                _context.Stocks.Remove(stock);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { activeTab = "Stocks" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshPrice(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock != null)
            {
                var price = await _stockPriceService.GetLatestPriceAsync(stock.Symbol);
                if (price.HasValue)
                {
                    stock.CurrentPrice = price.Value;
                    _context.Update(stock);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Price for {stock.Name} updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Could not fetch price for {stock.Symbol} via Yahoo Finance.";
                }
            }
            return RedirectToAction(nameof(Index), new { activeTab = "Stocks" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshAllPrices()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();

            int updatedCount = 0;
            foreach (var stock in stocks)
            {
                var price = await _stockPriceService.GetLatestPriceAsync(stock.Symbol);
                if (price.HasValue)
                {
                    stock.CurrentPrice = price.Value;
                    _context.Update(stock);
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Successfully updated {updatedCount} stock prices.";
            }
            else
            {
                TempData["ErrorMessage"] = "No prices could be updated via Yahoo Finance. Check your stock symbols.";
            }

            return RedirectToAction(nameof(Index), new { activeTab = "Stocks" });
        }


        // --- MUTUAL FUNDS ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMF(MutualFund mf)
        {
            mf.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            mf.PurchaseDate = mf.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Add(mf);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { activeTab = "Mutual Funds" });
            }
            return View("Create", mf);
        }

        public async Task<IActionResult> EditMF(int id)
        {
            var mf = await _context.MutualFunds.FindAsync(id);
            if (mf == null) return NotFound();
            if (IsAjax()) return PartialView(mf);
            return View(mf);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMF(int id, MutualFund mf)
        {
            if (id != mf.Id) return NotFound();
            mf.PurchaseDate = mf.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Update(mf);
                await _context.SaveChangesAsync();
                if (IsAjax()) return Json(new { success = true, type = "MutualFund" });
                return RedirectToAction(nameof(Index), new { activeTab = "Mutual Funds" });
            }
            if (IsAjax()) return PartialView(mf);
            return View(mf);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMF(int id)
        {
            var mf = await _context.MutualFunds.FindAsync(id);
            if (mf != null)
            {
                _context.MutualFunds.Remove(mf);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { activeTab = "Mutual Funds" });
        }

        // --- FIXED DEPOSITS ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFD(FixedDeposit fd)
        {
            fd.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            fd.PurchaseDate = fd.PurchaseDate.ToUniversalTime();
            fd.MaturityDate = fd.MaturityDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Add(fd);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { activeTab = "Fixed Deposits" });
            }

            // Repopulate banks on error
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            ViewBag.Banks = _context.AccountSources
                .Where(a => (isAdmin || a.UserId == userId) && a.Type == AccountType.Bank)
                .Select(a => a.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            return View("Create", fd);
        }

        public async Task<IActionResult> EditFD(int id)
        {
            var fd = await _context.FixedDeposits.FindAsync(id);
            if (fd == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var banks = await _context.AccountSources
                .Where(a => (isAdmin || a.UserId == userId) && a.Type == AccountType.Bank)
                .Select(a => a.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();
            ViewBag.Banks = banks;

            if (IsAjax()) return PartialView(fd);
            return View(fd);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFD(int id, FixedDeposit fd)
        {
            if (id != fd.Id) return NotFound();
            fd.PurchaseDate = fd.PurchaseDate.ToUniversalTime();
            fd.MaturityDate = fd.MaturityDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Update(fd);
                await _context.SaveChangesAsync();
                if (IsAjax()) return Json(new { success = true, type = "FixedDeposit" });
                return RedirectToAction(nameof(Index), new { activeTab = "Fixed Deposits" });
            }

            // Repopulate banks on error
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            ViewBag.Banks = await _context.AccountSources
                .Where(a => (isAdmin || a.UserId == userId) && a.Type == AccountType.Bank)
                .Select(a => a.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            if (IsAjax()) return PartialView(fd);
            return View(fd);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFD(int id)
        {
            var fd = await _context.FixedDeposits.FindAsync(id);
            if (fd != null)
            {
                _context.FixedDeposits.Remove(fd);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { activeTab = "Fixed Deposits" });
        }

        // --- NPS ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNPS(NPS nps)
        {
            nps.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            nps.PurchaseDate = nps.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Add(nps);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { activeTab = "NPS" });
            }
            return View("Create", nps);
        }

        public async Task<IActionResult> EditNPS(int id)
        {
            var nps = await _context.NPSs.FindAsync(id);
            if (nps == null) return NotFound();
            if (IsAjax()) return PartialView(nps);
            return View(nps);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNPS(int id, NPS nps)
        {
            if (id != nps.Id) return NotFound();
            nps.PurchaseDate = nps.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Update(nps);
                await _context.SaveChangesAsync();
                if (IsAjax()) return Json(new { success = true, type = "NPS" });
                return RedirectToAction(nameof(Index), new { activeTab = "NPS" });
            }
            if (IsAjax()) return PartialView(nps);
            return View(nps);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNPS(int id)
        {
            var nps = await _context.NPSs.FindAsync(id);
            if (nps != null)
            {
                _context.NPSs.Remove(nps);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { activeTab = "NPS" });
        }

        // AJAX Helper Actions for refreshing components
        public async Task<IActionResult> GetSummaryPartial()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var viewModel = new InvestmentsViewModel();
            
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();
            var mfs = await _context.MutualFunds.Where(m => isAdmin || m.UserId == userId).ToListAsync();
            var fds = await _context.FixedDeposits.Where(f => isAdmin || f.UserId == userId).ToListAsync();
            var npss = await _context.NPSs.Where(n => isAdmin || n.UserId == userId).ToListAsync();
            var epfs = await _context.EPFs.Where(e => isAdmin || e.UserId == userId).ToListAsync();

            viewModel.TotalStockValue = stocks.Sum(s => s.CurrentValue);
            viewModel.TotalMFValue = mfs.Sum(m => m.CurrentValue);
            viewModel.TotalFDValue = fds.Sum(f => f.CurrentValue);
            viewModel.TotalNPSValue = npss.Sum(n => n.CurrentValue);
            viewModel.TotalEPFValue = epfs.Sum(e => e.CurrentValue);
            
            viewModel.TotalStockInvested = stocks.Sum(s => s.TotalCost);
            viewModel.TotalMFInvested = mfs.Sum(m => m.TotalCost);
            viewModel.TotalNPSInvested = npss.Sum(n => n.TotalInvested);
            viewModel.TotalFDInvested = fds.Sum(f => f.PrincipalAmount);
            viewModel.TotalEPFInvested = epfs.Sum(e => e.EmployeeContribution);

            viewModel.TotalStockPL = stocks.Sum(s => s.ProfitLoss);
            viewModel.TotalMFPL = mfs.Sum(m => m.ProfitLoss);
            viewModel.TotalNPSPL = npss.Sum(n => n.ProfitLoss);
            viewModel.TotalFDPL = fds.Sum(f => f.ProfitLoss);
            viewModel.TotalEPFPL = epfs.Sum(e => e.ProfitLoss);

            return PartialView("_InvestmentSummary", viewModel);
        }

        public async Task<IActionResult> GetStockTable(string sortOrder)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();
            var sortedStocks = ApplyStockSort(stocks, sortOrder);
            return PartialView("_StockTable", sortedStocks);
        }

        public async Task<IActionResult> GetMFTable(string sortOrder)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var mfs = await _context.MutualFunds.Where(m => isAdmin || m.UserId == userId).ToListAsync();
            var sortedMfs = ApplyMFSort(mfs, sortOrder);
            return PartialView("_MFTable", sortedMfs);
        }

        public async Task<IActionResult> GetFDTable(string sortOrder)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var fds = await _context.FixedDeposits.Where(f => isAdmin || f.UserId == userId).ToListAsync();
            var sortedFds = ApplyFDSort(fds, sortOrder);
            return PartialView("_FDTable", sortedFds);
        }

        public async Task<IActionResult> GetNPSTable(string sortOrder)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var npss = await _context.NPSs.Where(n => isAdmin || n.UserId == userId).ToListAsync();
            var sortedNpss = ApplyNPSSort(npss, sortOrder);
            return PartialView("_NPSTable", sortedNpss);
        }

        public async Task<IActionResult> GetTechnicalAnalysis()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();
            
            var distinctSymbols = stocks.Select(s => s.Symbol).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            
            var results = new List<TechnicalAnalysisResult>();
            
            // Limit concurrency to prevent 429 Too Many Requests from Yahoo Finance
            var semaphore = new System.Threading.SemaphoreSlim(5);
            var tasks = distinctSymbols.Select(async symbol => 
            {
                await semaphore.WaitAsync();
                try
                {
                    return await _technicalAnalysisService.GetAnalysisAsync(symbol!);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var analysisArray = await Task.WhenAll(tasks);
            
            results.AddRange(analysisArray.Where(r => r != null)!);
            
            return PartialView("_TechnicalAnalysis", results);
        }

        [HttpGet]
        public async Task<IActionResult> GetFundamentalSymbols()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var stocks = await _context.Stocks.Where(s => isAdmin || s.UserId == userId).ToListAsync();
            
            var distinctSymbols = stocks.Select(s => s.Symbol).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            
            return Json(distinctSymbols);
        }

        [HttpPost]
        public async Task<IActionResult> GetFundamentalAnalysisChunk([FromBody] string[] symbols)
        {
            if (symbols == null || symbols.Length == 0) return PartialView("_FundamentalAnalysisRows", new List<FundamentalAnalysisResult>());

            var results = new List<FundamentalAnalysisResult>();
            
            // Limit concurrency to prevent 429 Too Many Requests from Yahoo Finance
            var semaphore = new System.Threading.SemaphoreSlim(5);
            var tasks = symbols.Select(async symbol => 
            {
                await semaphore.WaitAsync();
                try
                {
                    return await _fundamentalAnalysisService.GetAnalysisAsync(symbol!);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var analysisArray = await Task.WhenAll(tasks);
            var fetchedResults = analysisArray.Where(r => r != null).ToList();
            results.AddRange(fetchedResults);

            // Backfill any symbols that completely failed to return any fundamental data
            var fetchedSymbols = fetchedResults.Select(r => r.Symbol).ToHashSet();
            foreach (var symbol in symbols)
            {
                if (!fetchedSymbols.Contains(symbol))
                {
                    results.Add(new FundamentalAnalysisResult {
                        Symbol = symbol,
                        Category = "Error",
                        Reason = "No fundamental data available on Yahoo Finance."
                    });
                }
            }
            
            return PartialView("_FundamentalAnalysisRows", results.OrderBy(r => Array.IndexOf(symbols, r.Symbol)).ToList());
        }

        // --- EPF ---

        public IActionResult CreateEPF() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEPF(EPF epf)
        {
            epf.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            epf.PurchaseDate = epf.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Add(epf);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { activeTab = "EPF" });
            }
            return View("Create", epf);
        }

        public async Task<IActionResult> EditEPF(int id)
        {
            var epf = await _context.EPFs.FindAsync(id);
            if (epf == null) return NotFound();
            if (IsAjax()) return PartialView(epf);
            return View(epf);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEPF(int id, EPF epf)
        {
            if (id != epf.Id) return NotFound();
            epf.PurchaseDate = epf.PurchaseDate.ToUniversalTime();
            if (ModelState.IsValid)
            {
                _context.Update(epf);
                await _context.SaveChangesAsync();
                if (IsAjax()) return Json(new { success = true, type = "EPF" });
                return RedirectToAction(nameof(Index), new { activeTab = "EPF" });
            }
            if (IsAjax()) return PartialView(epf);
            return View(epf);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEPF(int id)
        {
            var epf = await _context.EPFs.FindAsync(id);
            if (epf != null)
            {
                _context.EPFs.Remove(epf);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { activeTab = "EPF" });
        }

        public async Task<IActionResult> GetEPFTable(string sortOrder)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var epfs = await _context.EPFs.Where(e => isAdmin || e.UserId == userId).ToListAsync();
            var sortedEpfs = ApplyEPFSort(epfs, sortOrder);
            return PartialView("_EPFTable", sortedEpfs);
        }

        private List<EPF> ApplyEPFSort(List<EPF> list, string sortOrder)
        {
            return sortOrder switch
            {
                "name_desc" => list.OrderByDescending(i => i.UAN).ToList(),
                "Price" => list.OrderBy(i => i.EmployeeContribution).ToList(),
                "price_desc" => list.OrderByDescending(i => i.EmployeeContribution).ToList(),
                "Cost" => list.OrderBy(i => i.EmployeeContribution).ToList(),
                "cost_desc" => list.OrderByDescending(i => i.EmployeeContribution).ToList(),
                "Value" => list.OrderBy(i => i.CurrentValue).ToList(),
                "value_desc" => list.OrderByDescending(i => i.CurrentValue).ToList(),
                "PL" => list.OrderBy(i => i.ProfitLoss).ToList(),
                "pl_desc" => list.OrderByDescending(i => i.ProfitLoss).ToList(),
                _ => list.OrderBy(i => i.UAN).ToList(),
            };
        }

        private bool IsAjax()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }
    }
}
