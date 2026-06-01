using Checkers.Engine;

namespace Checkers.Web.Models
{
    // TODO: На следующем шаге развернуть здесь живой контекст сессии
    // Реализовать метод ToRecordEntity() для сериализации истории ходов из Game в плоский JSON - снапшот для СУБД.
    /// <summary>
    /// Данные активной игровой сессии.
    /// </summary>
    public class MatchDetails
    {
        /// <summary>Уникальный короткий идентификатор матча для обмена между клиентами.</summary>
        public string MatchId { get; set; } = Guid.NewGuid().ToString()[..4].ToUpper();

        public MatchParticipant? WhiteParticipant { get; set; }
        public MatchParticipant? BlackParticipant { get; set; }
        public GameSession Game { get; init; }

        public MatchDetails(GameSession engineSession)
        {
            Game = engineSession;
        }
    }
}
