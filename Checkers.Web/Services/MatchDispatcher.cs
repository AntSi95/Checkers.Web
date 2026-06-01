using System.Collections.Concurrent;
using Checkers.Engine;
using Checkers.Engine.Rules;
using Checkers.Engine.Rules.Variants;
using Checkers.Engine.Scanning;
using Checkers.Web.Models;

namespace Checkers.Web.Services
{
    public interface IMatchDispatcher
    {
        MatchRoom CreateSession(string creatorConnectionId, MatchSettingsDto settings);
        MatchRoom? JoinSession(string matchId, string guestConnectionId);
        MatchRoom? GetSessionByPlayer(string connectionId);
        void RemoveSession(string matchId);
    }

    /// <summary>
    /// Диспетчер матчей (Singleton), выполняющий роль фабрики и менеджера жизненного цикла игровых комнат в памяти.
    /// </summary>
    public class MatchDispatcher : IMatchDispatcher
    {
        private readonly ConcurrentDictionary<string, MatchRoom> _activeMatches = new();

        /// <summary>
        /// Фабричный метод: собирает компоненты ядра игры и инициализирует новую комнату.
        /// </summary>
        public MatchRoom CreateSession(string creatorConnectionId, MatchSettingsDto settings)
        {
            // 1. Подбираем нужный класс правил ядра на основе выбора в лобби// Подбираем стратегию правил ядра на основе выбора пользователя в лобби
            IRulesStrategy rules = settings.Variant switch
            {
                "English" => new EnglishRules(),       // Английские шашки (Чекерс)
                "International" => new InternationalRules(), // Международные (Стоклеточные)
                "Russian" => new RussianRules(),       // Русские шашки
                _ => new RussianRules()        // Дефолт при некорректном DTO
            };

            var scanner = new MoveScanner();
            var game = new GameSession(rules, scanner);
            var matchRoom = new MatchRoom(game);

            if (!string.IsNullOrEmpty(creatorConnectionId))
            {
                matchRoom.WhiteConnectionId = creatorConnectionId;
            }

            _activeMatches.TryAdd(matchRoom.MatchId, matchRoom);
            return matchRoom;
        }

        public MatchRoom? JoinSession(string matchId, string guestConnectionId)
        {
            return _activeMatches.TryGetValue(matchId, out var matchRoom) ? matchRoom : null;
        }

        public MatchRoom? GetSessionByPlayer(string connectionId)
        {
            return _activeMatches.Values.FirstOrDefault(m =>
                m.WhiteConnectionId == connectionId || m.BlackConnectionId == connectionId);
        }

        public void RemoveSession(string matchId)
        {
            _activeMatches.TryRemove(matchId, out _);
        }
    }
}
