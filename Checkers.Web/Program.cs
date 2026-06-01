using Checkers.Web.Hubs;
using Checkers.Web.Infrastructure;
using Checkers.Web.Services;

namespace Checkers.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ====== ConfigureServices ======

            builder.Services.AddControllers();
            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    // Сериализация матриц игрового поля шашек [,]
                    options.PayloadSerializerOptions.Converters.Add(new Json2DArrayConverterFactory());
                });

            // TODO: Интегрировать IDbContextFactory для финального сохранения сессий
            // TODO: Подключить Redis/распределенный кэш для оперативной дозаписи ходов в реальном времени
            // TODO: Добавить Cookie/JWT аутентификацию для авторизации игроков

            builder.Services.AddSingleton<IMatchDispatcher, MatchDispatcher>();

            var app = builder.Build();

            // ========= Configure ===========

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // TODO: Подключить UseAuthentication() и UseAuthorization() перед маппингом эндпоинтов

            app.MapControllers();
            app.MapHub<MatchHub>("/matchhub");

            app.Run();
        }
    }
}
