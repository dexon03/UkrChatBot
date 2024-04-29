using Refit;
using UkrChatBot.Models;

namespace UkrChatBot.Client;

public interface IUkrMovaInUaApi
{
    [Get("/api-new?route=categories")]
    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default); 
}