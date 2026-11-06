using Microsoft.AspNetCore.Mvc;
using Practitioners.Api.Dtos;
using Practitioners.Api.Services;

namespace Practitioners.Api.Controllers;

/// <summary>Стандартный REST CRUD для справочника врачей.</summary>
[ApiController]
[Route("api/practitioners")]
[Produces("application/json")]
public class PractitionersController(PractitionerService service) : ControllerBase
{
    /// <summary>Создать врача.</summary>
    /// <remarks>
    /// Необязательный заголовок <c>Idempotency-Key</c> делает повтор запроса безопасным:
    /// при повторе с тем же ключом вернётся уже созданная запись (200), а не дубль.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(PractitionerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PractitionerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PractitionerDto>> Create(
        [FromBody] PractitionerRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (idempotencyKey is { Length: > 200 })
            return Problem("Idempotency-Key must be at most 200 characters.", statusCode: 400);

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        var (dto, created) = await service.CreateAsync(request, key, ct);
        SetETag(dto);

        if (!created) return Ok(dto);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Получить врача по идентификатору.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PractitionerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PractitionerDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await service.GetAsync(id, ct);
        if (dto is null) return NotFound();
        SetETag(dto);
        return dto;
    }

    /// <summary>Поиск и фильтрация врачей с пагинацией.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PractitionerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PractitionerDto>>> Search(
        [FromQuery] PractitionerSearchQuery query, CancellationToken ct)
    {
        var result = await service.SearchAsync(new PractitionerSearch
        {
            Name = query.Q,
            FirstName = query.FirstName,
            LastName = query.LastName,
            Speciality = query.Speciality,
            PlaceOfWork = query.PlaceOfWork,
            Email = query.Email,
            Phone = query.Phone,
            IsActive = query.IsActive,
            Sort = query.Sort,
            Offset = (query.Page - 1) * query.PageSize,
            Count = query.PageSize
        }, ct);

        return new PagedResult<PractitionerDto>(result.Items, result.Total, query.Page, query.PageSize);
    }

    /// <summary>Изменить данные врача (полная замена).</summary>
    /// <remarks>
    /// Необязательный заголовок <c>If-Match</c> (значение из ETag, например <c>W/"3"</c>) включает
    /// оптимистичную блокировку: если запись уже изменилась — 412 Precondition Failed.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PractitionerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<PractitionerDto>> Update(
        Guid id,
        [FromBody] PractitionerRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        int? expectedVersion = null;
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var raw = ifMatch.Trim();
            if (raw.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) raw = raw[2..];
            raw = raw.Trim('"');
            if (!int.TryParse(raw, out var parsed))
                return Problem("If-Match must contain a version, e.g. W/\"3\".", statusCode: 400);
            expectedVersion = parsed;
        }

        var (status, dto) = await service.UpdateAsync(id, request, expectedVersion, ct);
        switch (status)
        {
            case UpdateStatus.NotFound:
                return NotFound();
            case UpdateStatus.Conflict:
                if (dto is not null) SetETag(dto);
                return Problem(
                    "The practitioner was modified by someone else. Re-read it and retry.",
                    statusCode: expectedVersion is null ? 409 : 412);
            default:
                SetETag(dto!);
                return dto!;
        }
    }

    /// <summary>Удалить врача (soft delete). Идемпотентно: повторный вызов тоже вернёт 204.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    private void SetETag(PractitionerDto dto) => Response.Headers.ETag = $"W/\"{dto.Version}\"";
}
