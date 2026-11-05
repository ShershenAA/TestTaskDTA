using Microsoft.EntityFrameworkCore;

namespace Practitioners.Api.Data;

public static class DbInitializer
{
    /// <summary>
    /// Применяет EF Core миграции: схема (InitialCreate) и тестовые данные (SeedPractitioners).
    /// Повторные запуски безопасны — уже применённые миграции пропускаются.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider sp, ILogger log)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Count > 0)
                    log.LogInformation("Применяю миграции: {Migrations}", string.Join(", ", pending));

                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < 30)
            {
                log.LogWarning("БД ещё недоступна (попытка {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }
}
