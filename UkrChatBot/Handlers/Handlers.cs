using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Refit;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using UkrChatBot.Client;
using UkrChatBot.Models;
using UkrChatBot.Utils;

namespace UkrChatBot.Handlers;

public class Handlers
{
    private const string ApiUrl = "https://ukr-mova.in.ua";
    private static readonly UserState _state = new();

    public static async Task GetCategoriesAsync(ITelegramBotClient bot, long chatId, IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var redisCacheManager = new RedisCacheManager(configuration);
        var cachedCategories = await redisCacheManager.GetFromCacheAsync<List<Category>>("categories");

        if (cachedCategories == null)
        {
            var apiClient = RestService.For<IUkrMovaInUaApi>(ApiUrl);
            cachedCategories = await apiClient.GetCategoriesAsync(cancellationToken);
            await redisCacheManager.SetInCacheAsync("categories", cachedCategories,Constants.RedisExpiry);
        }

        if (cachedCategories.Count > 0)
        {
            var inlineKeyboardButtons = new List<List<InlineKeyboardButton>>();
            foreach (var category in cachedCategories)
            {
                inlineKeyboardButtons.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(category.Title, $"category_{category.Id}")
                });
            }

            var inlineKeyboardMarkup = new InlineKeyboardMarkup(inlineKeyboardButtons);

            await bot.SendTextMessageAsync(chatId, "Choose a category:", replyMarkup: inlineKeyboardMarkup,
                cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, "No categories available.", cancellationToken: cancellationToken);
        }
    }
    public static async Task HandleCategoryChoseAsync(ITelegramBotClient bot, long chatId, long categoryId,
        IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var examples = await GetExamples(configuration, cancellationToken);

        var examplesByCategory = examples?.Where(e => e.Category == categoryId);
        _state.ExamplesByCategory = examplesByCategory;
        var resultString = examplesByCategory.Select((e, i) => $"{i} - {e.Title}");
        await bot.SendTextMessageAsync(chatId, "Choose example by entering number:\n" + string.Join('\n', resultString),
            replyMarkup: new ForceReplyMarkup(),
            cancellationToken: cancellationToken);
    }

    public static async Task HandleExampleChooseAsync(ITelegramBotClient bot, long chatId, int exampleIndex,
        CancellationToken cancellationToken = default)
    {
        if (!_state.ExamplesByCategory.Any())
        {
            await bot.SendTextMessageAsync(chatId, "There is no examples", cancellationToken: cancellationToken);
            return;
        }

        var example = _state.ExamplesByCategory.ElementAt(exampleIndex);
        var img = new InputFileUrl(ApiUrl + "/" + example.Image);
        var text = $"{example.Title}\n" +
                   $"{example.Content}";
        string plainText = Regex.Replace(text, "<.*?>", String.Empty);

        await bot.SendPhotoAsync(chatId, img, caption: plainText, cancellationToken: cancellationToken);
    }

    public static async Task GetDailyRuleAsync(ITelegramBotClient bot, long chatId, IConfigurationRoot configuration,
        CancellationToken cancellationToken)
    {
        var examples = await GetExamples(configuration, cancellationToken);
        if (examples is null)
        {
            await bot.SendTextMessageAsync(chatId, "No examples available", cancellationToken: cancellationToken);
        }

        var day = DateTime.Now.Day;
        var random = new Random(day);
        var example = examples[random.Next(examples.Count)];
        
        var img = new InputFileUrl(ApiUrl + "/" + example.Image);
        var text = $"{example.Title}\n" +
                   $"{example.Content}";
        string plainText = Regex.Replace(text, "<.*?>", String.Empty);

        await bot.SendPhotoAsync(chatId, img, caption: plainText, cancellationToken: cancellationToken);
    }

    private static async Task<List<Example>?> GetExamples(IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var redisCacheManager = new RedisCacheManager(configuration);
        var cachedExamples = await redisCacheManager.GetFromCacheAsync<List<Example>>("examples");

        if (cachedExamples is null || cachedExamples.Count == 0)
        {
            var client = RestService.For<IUkrMovaInUaApi>(ApiUrl);
            var examples = await client.GetExamplesAsync(cancellationToken);
            await redisCacheManager.SetInCacheAsync("examples", examples, Constants.RedisExpiry);
        }
        
        return cachedExamples;
    }
}