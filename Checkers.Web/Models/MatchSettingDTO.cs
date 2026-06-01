namespace Checkers.Web.Models
{
    /// <summary>
    /// Конфигурация лобби, выбранная игроком в интерфейсе.
    /// </summary>
    public record MatchSettingsDto(
        string Variant          // "Russian", "International", "Pool"
    );
}