using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UkrChatBot.Database;
using UkrChatBot.Handlers;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

await MigrationAsync();

var logger = LoggerFactory.Create(builder =>
{
    builder.AddFilter(level => level >= LogLevel.Information);
}).CreateLogger("UkrChatBot");

var botClient = new TelegramBotClient(configuration.GetSection("TelegramBot")["Token"] ?? throw new InvalidOperationException());

using CancellationTokenSource cts = new ();


ReceiverOptions receiverOptions = new ()
{
    AllowedUpdates = Array.Empty<UpdateType>(),
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
{
    if (update.CallbackQuery is { } callbackQuery)
    {
        await bot.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        var parseResult = long.TryParse(callbackQuery.Data.Replace("category_", ""), out long categoryId);
        if (!parseResult)
        {
            logger.LogError("Couldn't parse categoryId");
            return;
        }
        
        //handle categoryId and return examples for this category
        await Handlers.HandleCategoryChoseAsync(bot, callbackQuery.Message.Chat.Id, categoryId, configuration);
        
        await bot.EditMessageReplyMarkupAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, 
            cancellationToken: cancellationToken);

        return;
    }
    if (update.Message is not { } message)
        return;
    
    if (message.Text is not { } messageText)
        return;
    
    var chatId = message.Chat.Id;

    switch (messageText)
    {
        case "/start":
            await AddStatisticsAsync("start");
            string textMessage = "Hello! I am a bot that will help you learn Ukrainian.\n " +
                             "Choose a category to get started.\n" +
                             "/categories - get a list of categories\n" +
                             "/dailyrule - get a daily rule";
            await botClient.SendTextMessageAsync(chatId, textMessage, cancellationToken: cancellationToken);
            
            break;
            
            
        case "/categories":
            await AddStatisticsAsync("categories");
            await Handlers.GetCategoriesAsync(bot, chatId, configuration, cancellationToken);
            break;
        case "/dailyrule":
            await AddStatisticsAsync("dailyrule");
            await Handlers.GetDailyRuleAsync(bot, chatId, configuration, cancellationToken);
            break;
        default:
            if (int.TryParse(messageText, out int selectedIndex))
            {
                await Handlers.HandleExampleChooseAsync(botClient, chatId, selectedIndex, cancellationToken);
            }
            break;
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };
    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

async Task MigrationAsync()
{
    using var dbContext = new ApplicationDbContext();
    var migrations = await dbContext.Database.GetPendingMigrationsAsync();
    if (migrations.Any())
    {
        await dbContext.Database.MigrateAsync();
    }
}

async Task AddStatisticsAsync(string command)
{
    await using var dbContext = new ApplicationDbContext();
    var handler = await dbContext.Handlers.FirstOrDefaultAsync(h => h.Title == command);
    if (handler != null)
    {
        handler.ClickedCount++;
        await dbContext.SaveChangesAsync();
    }
}