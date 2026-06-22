using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace TradeLedger.Services;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppSettings> Settings  => Set<AppSettings>();
    public DbSet<HoursEntry> HoursEntries => Set<HoursEntry>();
    public DbSet<Expense> Expenses => Set<Expense>();
}
