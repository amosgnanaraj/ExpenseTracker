using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Models
{
    public class QuickEntryViewModel
    {
        [Required(ErrorMessage = "Please select a date.")]
        [Display(Name = "Transaction Date")]
        public System.DateTime Date { get; set; } = System.DateTime.Now;

        [Required(ErrorMessage = "Please select a transaction type.")]
        [Display(Name = "Type")]
        public TransactionType Type { get; set; } = TransactionType.Debit;

        [Required(ErrorMessage = "Please enter an amount.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "Description (Optional)")]
        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Please select an account source.")]
        [Display(Name = "Account Source")]
        public int AccountSourceId { get; set; }

        [Display(Name = "Transfer To")]
        public int? TransferToAccountSourceId { get; set; }

        [Display(Name = "Salary Month")]
        public int? SalaryMonth { get; set; }

        [Display(Name = "Salary Year")]
        public int? SalaryYear { get; set; }

        // Data for dropdowns
        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> AccountSources { get; set; } = new List<SelectListItem>();

        // Data for History Grid
        public IEnumerable<Transaction> RecentTransactions { get; set; } = new List<Transaction>();
    }
}
