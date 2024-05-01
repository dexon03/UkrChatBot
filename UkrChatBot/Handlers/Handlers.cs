using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Refit;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using UkrChatBot.Client;
using UkrChatBot.Models;

namespace UkrChatBot.Handlers;

public class Handlers
{
    public static async Task GetCategoriesAsync(ITelegramBotClient bot, long chatId, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var categories = await GetCategories(configuration, cancellationToken);

        if (categories.Count > 0)
        {
            var inlineKeyboardButtons = new List<List<InlineKeyboardButton>>();
            foreach (var category in categories)
            {
                inlineKeyboardButtons.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(category.Title, $"category_{category.Id}")
                });
            }
            
            var inlineKeyboardMarkup = new InlineKeyboardMarkup(inlineKeyboardButtons);
            
            await bot.SendTextMessageAsync(chatId, "Choose a category:", replyMarkup: inlineKeyboardMarkup, cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, "No categories available.", cancellationToken: cancellationToken);
        }
    }

    private static async Task<List<Category>> GetCategories(IConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(configuration.GetConnectionString("Redis") ??
                                                                              throw new InvalidOperationException());
        var db = connection.GetDatabase();
        var cachedCategories = await db.StringGetAsync("categories");
        
        
        List<Category> categories;
    
        if (!cachedCategories.IsNull)
        {
            // Categories found in cache, deserialize and use them
            categories = JsonConvert.DeserializeObject<List<Category>>(cachedCategories);
        }
        else
        {
            var client = RestService.For<IUkrMovaInUaApi>("https://ukr-mova.in.ua");
            categories = await client.GetCategoriesAsync(cancellationToken);
            
            await db.StringSetAsync("categories", JsonConvert.SerializeObject(categories), expiry: TimeSpan.FromMinutes(30));
        }
        
        return categories;
    }

    public static async Task HandleCategoryChose(ITelegramBotClient bot, long chatId, string categoryId, CancellationToken cancellationToken = default)
    {
        
    }
}