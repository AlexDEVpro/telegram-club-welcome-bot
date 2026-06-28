using System.Globalization;

namespace TelegramClubWelcomeBot.Infrastructure.Localization.Services;

internal sealed class CultureContextManager
{
    public IDisposable Use(CultureInfo culture)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        return new Scope(() =>
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        });
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _dispose;

        public Scope(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose();
        }
    }
}
