using Data.Models;
using Microsoft.EntityFrameworkCore;

// This class represents a connection to the database and exposes the tables.

namespace SecretProject.Services;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<HoursEntry> HoursEntries => Set<HoursEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HoursEntry>().HasIndex(h => h.Date).IsUnique();
    }
}

/* line 15 The unique index on 'Date' enforces 'one row per day' at the database level
   - it will prevent two entries for the same date */

/* line 2 using library EFCore, 
   line 6 naming convention for namespaces, naming after the file name 
   lines 10 and 11, using the library variables creating database sets, using the
   data models created in Models/AppSettings, Models/HoursEntry */