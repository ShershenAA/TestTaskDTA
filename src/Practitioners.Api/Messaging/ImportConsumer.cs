using System.Text.Json;
using Practitioners.Api.Infrastructure;
using Practitioners.Api.Services;
using StackExchange.Redis;

namespace Practitioners.Api.Messaging;

/// <summary>
/// Consumer событий импорта (Redis Streams + consumer group).
/// At-least-once доставка + идемпотентная обработка (таблица processed_events) = effectively-once.
/// </summary>
public class ImportConsumer(
    IConnectionMultiplexer mux,
    IServiceScopeFactory scopes,
    IConfiguration cfg,
    ILogger<ImportConsumer> log) : BackgroundService
{
    private static readonly TimeSpan PendingRecheck = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var consumerName = cfg["Consumer:Name"] ?? Environment.MachineName;
        var db = mux.GetDatabase();

        await EnsureGroupAsync(db, ct);
        log.LogInformation("Consumer '{Name}' запущен, stream={Stream}, group={Group}",
            consumerName, StreamNames.Import, StreamNames.Group);

        var lastPendingCheck = DateTime.MinValue;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Периодически перечитываем свои неподтверждённые (pending) сообщения —
                // так повторно обрабатываются события, упавшие из-за временной ошибки.
                var readPending = DateTime.UtcNow - lastPendingCheck > PendingRecheck;
                var position = readPending ? StreamPosition.Beginning : StreamPosition.NewMessages;
                if (readPending) lastPendingCheck = DateTime.UtcNow;

                var entries = await db.StreamReadGroupAsync(
                    StreamNames.Import, StreamNames.Group, consumerName, position, count: 20);

                if (entries.Length == 0)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    try
                    {
                        await HandleAsync(db, entry, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        // не подтверждаем — сообщение останется в pending и будет обработано повторно
                        log.LogError(ex, "Ошибка обработки сообщения {Id}, будет повторена", entry.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Ошибка чтения из stream, повтор через 2 секунды");
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private async Task EnsureGroupAsync(IDatabase db, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await db.StreamCreateConsumerGroupAsync(StreamNames.Import, StreamNames.Group, StreamPosition.Beginning);
                return;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                return; // группа уже существует
            }
            catch (Exception ex)
            {
                log.LogWarning("Redis недоступен: {Message}. Повтор...", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private async Task HandleAsync(IDatabase db, StreamEntry entry, CancellationToken ct)
    {
        var raw = entry.Values.FirstOrDefault(v => v.Name == "payload").Value;

        ImportEvent? evt = null;
        if (!raw.IsNullOrEmpty)
        {
            try { evt = JsonSerializer.Deserialize<ImportEvent>(raw.ToString(), JsonDefaults.Options); }
            catch (JsonException) { /* уйдёт в DLQ ниже */ }
        }

        if (evt is null || string.IsNullOrWhiteSpace(evt.EventId) || evt.Payload is null)
        {
            await DeadLetterAsync(db, entry, raw, "malformed message");
            return;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PractitionerService>();
            var applied = await service.ApplyImportAsync(evt, ct);

            if (applied) log.LogInformation("Событие {EventId} ({Type}) применено", evt.EventId, evt.Type);
            else log.LogInformation("Событие {EventId} уже было обработано — пропуск (идемпотентность)", evt.EventId);
        }
        catch (InvalidDataException ex)
        {
            // невалидные данные ретраить бессмысленно
            await DeadLetterAsync(db, entry, raw, ex.Message);
            return;
        }

        await db.StreamAcknowledgeAsync(StreamNames.Import, StreamNames.Group, entry.Id);
    }

    private async Task DeadLetterAsync(IDatabase db, StreamEntry entry, RedisValue raw, string error)
    {
        log.LogWarning("Сообщение {Id} отправлено в DLQ: {Error}", entry.Id, error);
        await db.StreamAddAsync(StreamNames.DeadLetter, new[]
        {
            new NameValueEntry("payload", raw),
            new NameValueEntry("error", error),
            new NameValueEntry("originalId", entry.Id.ToString())
        });
        await db.StreamAcknowledgeAsync(StreamNames.Import, StreamNames.Group, entry.Id);
    }
}
