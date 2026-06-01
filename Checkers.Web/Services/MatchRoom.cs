using Checkers.Engine;
using Checkers.Engine.Models;

namespace Checkers.Web.Services
{
    /// <summary>
    /// Игровая комната, управляющая циклом конкретного матча и валидацией команд в памяти сервера.
    /// </summary>
    public class MatchRoom
    {
        /// <summary>Уникальный короткий идентификатор матча.</summary>
        public string MatchId { get; } = Guid.NewGuid().ToString()[..4].ToUpper();

        /// <summary>Текущий идентификатор WebSocket-соединения игрока за белых.</summary>
        public string? WhiteConnectionId { get; set; }

        /// <summary>Текущий идентификатор WebSocket-соединения игрока за черных.</summary>
        public string? BlackConnectionId { get; set; }

        private readonly GameSession _game;

        public MatchRoom(GameSession game)
        {
            _game = game;
        }

        /// <summary>Запрашивает актуальный легкий срез данных (состояние доски, счет, статусы) из игрового ядра.</summary>
        public SessionInfo GetMatchInfo() => _game.GetInfo();

        /// <summary> Запрашивает список легальных ходов для конкретного цвета фигур.</summary>
        public List<Move> GetValidMovesForPlayer(string connectionId)
        {
            var side = GetPlayerSide(connectionId);
            if (side == null) return [];
            return _game.GetValidMoves(side.Value);
        }

        /// <summary>
        /// Определяет сторону (цвет фигур) игрока на основе его текущего SignalR ConnectionId.
        /// </summary>
        public PieceSide? GetPlayerSide(string connectionId)
        {
            if (connectionId == WhiteConnectionId) return PieceSide.White;
            if (connectionId == BlackConnectionId) return PieceSide.Black;
            return null;
        }

        /// <summary>
        /// Проверяет права игрока, очередность хода и делегирует выполнение перемещения фигуры в ядро.
        /// </summary>
        public bool TryExecuteMove(string connectionId, Move move, out string error)
        {
            error = string.Empty;

            // 1. Проверяем права сетевого подключения
            var side = GetPlayerSide(connectionId);
            if (side == null)
            {
                error = "Вы не являетесь участником этого матча.";
                return false;
            }

            if (_game.ActiveSide != side)
            {
                error = "Сейчас ход другого игрока.";
                return false;
            }

            // TODO: Пока что так. Позже переделать вместе с полноценным логированием.
            // Как вариант сделать контракт ответа TryMoveResult. Что бы не вызывать лишние исключения
            // 2. Делегируем всю валидацию правил шашек в ядро.
            try
            {
                _game.MakeMove(side.Value, move);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                // Перехватываем "Указанный ход не входит в список допустимых перемещений" из ядра
                error = ex.Message;
                return false;
            }
        }
    }
}
