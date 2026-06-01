using Microsoft.AspNetCore.SignalR;
using Checkers.Engine.Models;
using Checkers.Web.Services;
using Checkers.Web.Models;

namespace Checkers.Web.Hubs
{
    /// <summary>
    /// Сетевой шлюз (Hub) для двустороннего real-time взаимодействия с игроками во время запущенного матча.
    /// </summary>
    /// <remarks>
    /// Внимание: Данный класс выполняет роль ИНФРАСТРУКТУРНОГО ПРЕДСТАВЛЕНИЯ (транспортного курьера). 
    /// Он отвечает за сериализацию сетевых пакетов, RPC-вызовы фронтенда и маршрутизацию SignalR-событий.
    /// Не стоит размещать здесь бизнес-логику игры, валидацию ходов, менеджмент состояний комнат или работу с СУБД.
    /// Любое действие должно делегироваться в <see cref="IMatchDispatcher"/> и соответствующие комнаты.
    /// </remarks>
    public class MatchHub : Hub
    {
        private readonly IMatchDispatcher _matchDispatcher;

        public MatchHub(IMatchDispatcher matchDispatcher)
        {
            _matchDispatcher = matchDispatcher;
        }

        /// <summary>
        /// Подключает клиента к игровой комнате, распределяет цвета фигур и инициирует старт матча при заполнении сессии.
        /// </summary>
        /// <param name="roomId">Уникальный идентификатор игровой комнаты.</param>
        /// <returns>Отправляет "RoomCreated", "GameStarted" со слепком поля и "YourTurn" со списком ходов.</returns>
        public async Task JoinRoom(string roomId)
        {
            var matchRoom = _matchDispatcher.JoinSession(roomId, Context.ConnectionId);
            if (matchRoom == null)
            {
                await Clients.Caller.SendAsync("Error", "Матч не найден.");
                return;
            }

            // TODO: Перенести эту логику распределения ролей (White/Black) внутрь метода matchRoom.JoinPlayer(...)
            // Хаб должен получать только результат (успешно/какой цвет/игра готова к старту)

            // Первый подключившийся сокет занимает Белых
            if (string.IsNullOrEmpty(matchRoom.WhiteConnectionId))
            {
                matchRoom.WhiteConnectionId = Context.ConnectionId;
                await Clients.Caller.SendAsync("RoomCreated", matchRoom.MatchId);
                return;
            }

            // Защита от дублирования: создатель пытается зайти второй раз в ту же сессию
            if (matchRoom.WhiteConnectionId == Context.ConnectionId) return;

            // Второй подключившийся сокет занимает Черных
            if (string.IsNullOrEmpty(matchRoom.BlackConnectionId))
            {
                matchRoom.BlackConnectionId = Context.ConnectionId;
            }
            else if (matchRoom.BlackConnectionId != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("Error", "В матче уже нет свободных мест.");
                return;
            }

            // СТАРТ ИГРЫ (Когда успешно заняты обе сетевые розетки)
            var sessionInfo = matchRoom.GetMatchInfo();

            await Clients.Client(matchRoom.WhiteConnectionId).SendAsync("GameStarted", "White", sessionInfo);
            await Clients.Client(matchRoom.BlackConnectionId).SendAsync("GameStarted", "Black", sessionInfo);

            // Белые всегда начинают партию: генерируем и отправляем им первый пакет валидных ходов
            var whiteMoves = matchRoom.GetValidMovesForPlayer(matchRoom.WhiteConnectionId);
            await Clients.Client(matchRoom.WhiteConnectionId).SendAsync("YourTurn", whiteMoves);
        }

        /// <summary>
        /// Принимает объект хода от игрока, передает его на просчет в ядро и рассылает обновленное состояние доски.
        /// </summary>
        /// <param name="move">Ядерная модель хода <see cref="Move"/>.</param>
        public async Task SendMove(Move move)
        {
            var matchRoom = _matchDispatcher.GetSessionByPlayer(Context.ConnectionId);
            if (matchRoom == null) return;

            // Передаем объект хода целиком. Вся валидация правил и двойные циклы скрыты внутри комнаты и ядра!
            if (matchRoom.TryExecuteMove(Context.ConnectionId, move, out string error))
            {
                var updatedSession = matchRoom.GetMatchInfo();

                // Рассылаем свежий слепок доски обоим игрокам по их ConnectionId
                await Clients.Client(matchRoom.WhiteConnectionId!).SendAsync("UpdateGameState", updatedSession);
                await Clients.Client(matchRoom.BlackConnectionId!).SendAsync("UpdateGameState", updatedSession);

                // Если игра завершилась матом, заблокированием или сдачей
                if (updatedSession.Status != GameStatus.InProgress)
                {
                    string endMessage = $"🏆 Игра окончена! Результат: {updatedSession.Status}. Причина: {updatedSession.Reason}";
                    await Clients.Client(matchRoom.WhiteConnectionId!).SendAsync("GameOver", endMessage);
                    await Clients.Client(matchRoom.BlackConnectionId!).SendAsync("GameOver", endMessage);

                    // TODO: Перед удалением вызвать метод финального сохранения JSON-снапшота матча в БД из MatchDispatcher
                    _matchDispatcher.RemoveSession(matchRoom.MatchId);
                    return;
                }

                // Определяем сокет следующего игрока по ActiveSide из ядра
                string nextPlayerId = updatedSession.ActiveSide == PieceSide.White
                    ? matchRoom.WhiteConnectionId!
                    : matchRoom.BlackConnectionId!;

                // Просим комнату посчитать легальные ходы для следующего игрока и отправляем пакет "YourTurn"
                var nextValidMoves = matchRoom.GetValidMovesForPlayer(nextPlayerId);
                await Clients.Client(nextPlayerId).SendAsync("YourTurn", nextValidMoves);
            }
            else
            {
                // Если ядро игры выплюнуло ошибку или ходил не тот игрок — отправляем Caller-у текст проблемы
                await Clients.Caller.SendAsync("Error", error);
            }
        }

        /// <summary>
        /// Перехватывает событие обрыва сетевого соединения с клиентом и детерминированно очищает сессию.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var matchRoom = _matchDispatcher.GetSessionByPlayer(Context.ConnectionId);
            if (matchRoom != null)
            {
                // TODO: На следующем шаге прикрутить сюда вызов метода комнаты room.HandleDisconnect() с таймером на 2 минуты.
                // Мгновенное удаление сессии переделать на экстренное сохранение состояния, если таймер ожидания игрока истек.
                string disconnectMessage = "Матч прерван из-за отключения одного из участников.";

                if (matchRoom.WhiteConnectionId != null)
                    await Clients.Client(matchRoom.WhiteConnectionId).SendAsync("GameOver", disconnectMessage);

                if (matchRoom.BlackConnectionId != null)
                    await Clients.Client(matchRoom.BlackConnectionId).SendAsync("GameOver", disconnectMessage);

                _matchDispatcher.RemoveSession(matchRoom.MatchId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
