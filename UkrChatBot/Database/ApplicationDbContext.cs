using Microsoft.EntityFrameworkCore;
using UkrChatBot.Models;

namespace UkrChatBot.Database;

public class ApplicationDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=UkrChatBotDb;Username=postgres;Password=postgres");
    }

    public DbSet<Handler> Handlers { get; set; }
}