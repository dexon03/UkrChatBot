using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Refit;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using UkrChatBot.Client;
using UkrChatBot.Models;

namespace UkrChatBot.Handlers;

public class Handlers
{
    private const string ApiUrl = "https://ukr-mova.in.ua";
    private static readonly UserState _state = new();
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
            var client = RestService.For<IUkrMovaInUaApi>(ApiUrl);
            categories = await client.GetCategoriesAsync(cancellationToken);
            
            await db.StringSetAsync("categories", JsonConvert.SerializeObject(categories), expiry: TimeSpan.FromMinutes(30));
        }
        
        return categories;
    }

    public static async Task HandleCategoryChoseAsync(ITelegramBotClient bot, long chatId, long categoryId, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(configuration.GetConnectionString("Redis") ??
                                                                              throw new InvalidOperationException());
        var db = connection.GetDatabase();
        var cachedExamples = await db.StringGetAsync("examples");

        List<Example> examples;

        if (!cachedExamples.IsNull)
        {
            examples = JsonConvert.DeserializeObject<List<Example>>(cachedExamples);
        }
        else
        {
            var client = RestService.For<IUkrMovaInUaApi>(ApiUrl);

            examples = await client.GetExamplesAsync(cancellationToken);

            await db.StringSetAsync("examples", JsonConvert.SerializeObject(examples),
                expiry: TimeSpan.FromMinutes(30));
        }

        var examplesByCategory = examples?.Where(e => e.Category == categoryId);
        _state.ExamplesByCategory = examplesByCategory; 
        var resultString = examplesByCategory.Select((e, i) => $"{i} - {e.Title}");
        await bot.SendTextMessageAsync(chatId, "Choose example by entering number:\n" + string.Join('\n',resultString),
            replyMarkup: new ForceReplyMarkup(),
            cancellationToken: cancellationToken);
    }

    public static async Task HandleExampleChooseAsync(ITelegramBotClient bot, long chatId, int exampleIndex,
        CancellationToken cancellationToken = default)
    {
        if (!_state.ExamplesByCategory.Any())
        {
            // display error
            await bot.SendTextMessageAsync(chatId, "There is no examples", cancellationToken: cancellationToken);
            return;
        }

        var example = _state.ExamplesByCategory.ElementAt(exampleIndex);
        var img = new InputFileUrl(ApiUrl + "/" + example.Image);
        var text = $"{example.Title}\n" +
                   $"{example.Content}";
        string plainText = Regex.Replace(text, "<.*?>", String.Empty);

        await bot.SendPhotoAsync(chatId, img, caption:plainText, cancellationToken: cancellationToken);
    }
}