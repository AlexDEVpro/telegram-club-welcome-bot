using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Telegram.Bot;

using TelegramClubWelcomeBot.Constants;
using TelegramClubWelcomeBot.Features.AddWelcome.Services;
using TelegramClubWelcomeBot.Features.NewMember.Services;
using TelegramClubWelcomeBot.Infrastructure.Configuration;
using TelegramClubWelcomeBot.Infrastructure.Localization.Services;
using TelegramClubWelcomeBot.Infrastructure.Notifications;
using TelegramClubWelcomeBot.Infrastructure.Telegram.Services;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration.
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables();

        // Options.
        builder.Services.Configure<TelegramOptions>(
            builder.Configuration.GetSection("TelegramOptions"));
        builder.Services.Configure<WelcomeOptions>(
            builder.Configuration.GetSection("WelcomeOptions"));

        // MediatR.
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });

        // Services.
        builder.Services.AddSingleton<StorageService>();
        builder.Services.AddSingleton<TelegramUpdateRouter>();
        builder.Services.AddSingleton<CultureContextManager>();
        builder.Services.AddSingleton<UserCultureResolver>();
        builder.Services.AddSingleton<WizardStateService>();
        builder.Services.AddSingleton<WelcomeSender>();
        builder.Services.AddSingleton<DevNotifier>();

        builder.Services.AddHostedService<SetBotCommandsService>();

        // Telegram client.
        builder.Services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>()
                .Value;

            if (string.IsNullOrWhiteSpace(options.BotToken))
            {
                throw new InvalidOperationException("Bot token is not specified.");
            }

            return new TelegramBotClient(options.BotToken);
        });

        var host = builder.Build();

        // Culture.
        var welcomeOptions = host.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<WelcomeOptions>>()
            .Value;
        var culture = new CultureInfo(SupportedLanguages.Get(welcomeOptions.DefaultLanguageCode).Culture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Data init.
        var storage = host.Services.GetRequiredService<StorageService>();
        storage.CleanupRecentJoins(welcomeOptions.WelcomeRepeatCooldownMin);

        // Bot start.
        var bot = host.Services.GetRequiredService<ITelegramBotClient>();
        var router = host.Services.GetRequiredService<TelegramUpdateRouter>();
        bot.StartReceiving(router.Update, router.Error);

        Console.WriteLine($"Bot started with culture: {culture.Name}");

        await host.RunAsync();
    }
}
