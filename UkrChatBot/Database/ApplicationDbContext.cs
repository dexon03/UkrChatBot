using Microsoft.EntityFrameworkCore;
using UkrChatBot.Models;

namespace UkrChatBot.Database;

public class ApplicationDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Handler>().HasData(
            new Handler { Id = 1, Title = "start", ClickedCount = 0 },
            new Handler { Id = 2, Title = "categories", ClickedCount = 0 },
            new Handler { Id = 3, Title = "dailyrule", ClickedCount = 0 }
        );
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=UkrChatBotDb;Username=postgres;Password=postgres");
    }

    public DbSet<Handler> Handlers { get; set; }
}