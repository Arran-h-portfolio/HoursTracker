namespace Data.Models;

public enum ExpenseCategory { Materials = 0, Travel = 1, Equipment = 2, Other = 3 }

public class Expense
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public double Amount { get; set; }
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public string Description { get; set; } = string.Empty;
}
