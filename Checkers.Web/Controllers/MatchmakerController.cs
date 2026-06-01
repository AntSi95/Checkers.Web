using Checkers.Web.Models;
using Checkers.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Checkers.Web.Controllers
{
    /// <summary>
    /// Точка входа HTTP API для инициализации и администрирования игровых сессий.
    /// </summary>
    /// <remarks>
    /// Внимание: Данный класс является ИНФРАСТРУКТУРНЫМ КОНТРОЛЛЕРОМ уровня представления. 
    /// Отвечает исключительно за парсинг HTTP-запросов (например, от Fetch API на фронтенде) и возврат стандартных статус-кодов.
    /// Вся бизнес-логика поиска соперников и создания игровых комнат инкапсулирована за фасадом <see cref="IMatchDispatcher"/>.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    public class MatchmakerController : ControllerBase
    {
        private readonly IMatchDispatcher _matchDispatcher;

        public MatchmakerController(IMatchDispatcher matchDispatcher)
        {
            _matchDispatcher = matchDispatcher;
        }

        // POST эндпоинт: /api/matchmaker/create
        [HttpPost("create")]
        public IActionResult CreateMatch([FromBody] MatchSettingsDto settings)
        {
            // Передаем настройки лобби в диспетчер
            var matchRoom = _matchDispatcher.CreateSession(string.Empty, settings);

            return Ok(new { matchId = matchRoom.MatchId });
        }
    }
}
