namespace Checkers.Web.Models
{
    // TODO: На следующем шаге связать эту сущность с полноценной СУБД
    // Использовать этот класс для хранения долговечных доменных данных (Id, Nickname, Рейтинг), 
    // полностью исключив отсюда временные инфраструктурные ConnectionId от SignalR.
    // Перевести UserId со строки на Guid? после создания таблицы Users.
    /// <summary>
    /// Паспорт участника матча (заготовка сущности игрока для будущей БД).
    /// </summary>
    public class MatchParticipant
    {
        /// <summary>Уникальный ID пользователя из будущей базы данных (null для гостей).</summary>
        public string? UserId { get; set; }

        /// <summary>Отображаемое имя игрока.</summary>
        public string Nickname { get; set; }

        public bool IsGuest => string.IsNullOrEmpty(UserId);

        public MatchParticipant(string connectionId, string nickname, string? userId = null)
        {
            Nickname = nickname;
            UserId = userId;
        }
    }
}
