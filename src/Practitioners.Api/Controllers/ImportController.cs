using Microsoft.AspNetCore.Mvc;
using Practitioners.Api.Messaging;

namespace Practitioners.Api.Controllers;

/// <summary>Producer: принимает пачку событий и публикует их в брокер (Redis Stream). Обработка — в consumer'е.</summary>
[ApiController]
[Route("api/import/practitioners")]
[Produces("application/json")]
public class ImportController(ImportPublisher publisher) : ControllerBase
{
    public record ImportAccepted(int Accepted, IReadOnlyList<string> EventIds);

    /// <summary>Асинхронный импорт врачей через брокер сообщений.</summary>
    /// <remarks>
    /// Каждое событие: <c>eventId</c> (необязателен, при повторной отправке используйте тот же — дубль будет проигнорирован),
    /// <c>type</c> = upsert | delete, <c>occurredAt</c>, <c>payload</c>.
    /// Ответ 202 Accepted: данные применяются consumer'ом асинхронно.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ImportAccepted), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Import([FromBody] List<ImportEvent> events)
    {
        if (events.Count is 0 or > 1000)
            return Problem("Send between 1 and 1000 events.", statusCode: 400);

        foreach (var e in events)
        {
            if (e.Payload is null)
                return Problem("Every event must have a payload.", statusCode: 400);
            if (!string.Equals(e.Type, "upsert", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(e.Type, "delete", StringComparison.OrdinalIgnoreCase))
                return Problem($"Unknown event type '{e.Type}'. Use upsert or delete.", statusCode: 400);
            if (string.IsNullOrWhiteSpace(e.EventId))
                e.EventId = Guid.NewGuid().ToString();
        }

        foreach (var e in events)
            await publisher.PublishAsync(e);

        return Accepted(new ImportAccepted(events.Count, events.Select(e => e.EventId!).ToList()));
    }
}
