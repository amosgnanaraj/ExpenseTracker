using System.Security.Claims;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class AccountSourcesController : Controller
    {
        private readonly ExpenseTrackerDbContext _context;

        public AccountSourcesController(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        // GET: AccountSources
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var accounts = await _context.AccountSources
                .Where(a => isAdmin || a.UserId == userId)
                .OrderBy(a => a.Type)
                .ThenBy(a => a.Name)
                .ToListAsync();

            return View(accounts);
        }

        // GET: AccountSources/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AccountSources/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Type,Balance,InterestRate,MinimumPayment,AccountNumber")] AccountSource accountSource)
        {
            // Optional Interest Rate logic validation:
            if (accountSource.Type != AccountType.Loan)
            {
                accountSource.InterestRate = null; // Enforce null if not a loan
            }

            if (ModelState.IsValid)
            {
                accountSource.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _context.Add(accountSource);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(accountSource);
        }

        // GET: AccountSources/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var accountSource = await _context.AccountSources.FindAsync(id);
            if (accountSource == null || (!isAdmin && accountSource.UserId != userId))
            {
                return NotFound();
            }

            if (isAdmin)
            {
                var users = await _context.Users.ToListAsync();
                ViewBag.Users = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(users, "Id", "Email", accountSource.UserId);
            }

            return View(accountSource);
        }

        // POST: AccountSources/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Type,Balance,InterestRate,MinimumPayment,UserId,AccountNumber")] AccountSource accountSource)
        {
            if (id != accountSource.Id)
            {
                return NotFound();
            }

            if (accountSource.Type != AccountType.Loan)
            {
                accountSource.InterestRate = null;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingAccount = await _context.AccountSources.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
                    if (existingAccount == null) return NotFound();

                    var isAdmin = User.IsInRole("Admin");
                    if (!isAdmin)
                    {
                        // Maintain original ownership if not admin
                        accountSource.UserId = existingAccount.UserId;
                    }

                    _context.Update(accountSource);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountSourceExists(accountSource.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            if (User.IsInRole("Admin"))
            {
                var users = await _context.Users.ToListAsync();
                ViewBag.Users = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(users, "Id", "Email", accountSource.UserId);
            }

            return View(accountSource);
        }

        // GET: AccountSources/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var accountSource = await _context.AccountSources
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (accountSource == null || (!isAdmin && accountSource.UserId != userId))
            {
                return NotFound();
            }

            return View(accountSource);
        }

        // POST: AccountSources/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var accountSource = await _context.AccountSources.FindAsync(id);
            if (accountSource != null)
            {
                _context.AccountSources.Remove(accountSource);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AccountSourceExists(int id)
        {
            return _context.AccountSources.Any(e => e.Id == id);
        }
    }
}
