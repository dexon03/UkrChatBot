using Refit;
using UkrChatBot.Models;

namespace UkrChatBot.Client;

public interface IUkrMovaInUaApi
{
    [Get("/api-new?route=categories")]
    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    
    [Get("/api-new?route=examples")]
    Task<List<Example>> GetExamplesAsync(CancellationToken cancellationToken = default);
}