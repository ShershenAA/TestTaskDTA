using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Practitioners.Api.Data;
using Practitioners.Api.Domain;
using Practitioners.Api.Dtos;
using Practitioners.Api.Infrastructure;
using Practitioners.Api.Messaging;

namespace Practitioners.Api.Services;

public enum UpdateStatus { Ok, NotFound, Conflict }

public class PractitionerService(AppDbContext db, ICacheService cache, ILogger<PractitionerService> log)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static string ItemKey(Guid id) => $"prac:item:{id}";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    #region CREATE
   
    /// <summary>Создание. Если передан idempotencyKey, повторный вызов вернёт ту же запись, а не создаст дубль.</summary>
    public async Task<(PractitionerDto Dto, bool Created)> CreateAsync(
        PractitionerRequest r, string? idempotencyKey, CancellationToken ct)
    {
        if (idempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null) return (existing, false);
        }

        var now = DateTime.UtcNow;
        var entity = new Practitioner { Id = Guid.NewGuid(), Created = now, Updated = now, Version = 1 };
        Apply(entity, r);
        db.Practitioners.Add(entity);

        if (idempotencyKey is not null)
            db.IdempotencyKeys.Add(new IdempotencyKey { Key = idempotencyKey, PractitionerId = entity.Id, Created = now });

        try
        {
            // запись и ключ идемпотентности сохраняются атомарно
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (idempotencyKey is not null && IsUniqueViolation(ex))
        {
            // параллельный запрос с тем же ключом успел раньше
            db.ChangeTracker.Clear();
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null) return (existing, false);
            throw;
        }

        await cache.BumpListVersionAsync();
        return (ToDto(entity), true);
    }

    private async Task<PractitionerDto?> FindByIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        var practitionerId = await db.IdempotencyKeys.AsNoTracking()
            .Where(k => k.Key == key)
            .Select(k => (Guid?)k.PractitionerId)
            .FirstOrDefaultAsync(ct);

        if (practitionerId is null) return null;

        var e = await db.Practitioners.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == practitionerId, ct);
        return e is null ? null : ToDto(e);
    }

    #endregion
    
    #region GET

    public async Task<PractitionerDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var cached = await cache.GetAsync<PractitionerDto>(ItemKey(id));
        if (cached is not null) return cached;

        var e = await db.Practitioners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;

        var dto = ToDto(e);
        await cache.SetAsync(ItemKey(id), dto, CacheTtl);
        return dto;
    }

    /// <summary>Для FHIR: отличить 410 Gone (удалён) от 404 (не существовал).</summary>
    public Task<bool> IsDeletedAsync(Guid id, CancellationToken ct) =>
        db.Practitioners.IgnoreQueryFilters().AnyAsync(x => x.Id == id && x.IsDeleted, ct);
    
    #endregion

    #region SEARCH

    // ---------------------------------------------------------------- 

    public async Task<SearchResult> SearchAsync(PractitionerSearch s, CancellationToken ct)
    {
        var count = Math.Clamp(s.Count, 1, 100);
        var offset = Math.Max(s.Offset, 0);

        var cacheKey = $"prac:list:{await cache.GetListVersionAsync()}:{Hash(s)}";
        var cached = await cache.GetAsync<SearchResult>(cacheKey);
        if (cached is not null) return cached;

        IQueryable<Practitioner> q = db.Practitioners.AsNoTracking();

        if (s.Id is { } id)
            q = q.Where(x => x.Id == id);

        if (!string.IsNullOrWhiteSpace(s.Name))
        {
            var tsQuery = BuildTsQuery(s.Name);
            if (tsQuery is null) return new SearchResult([], 0);
            q = q.Where(x => x.SearchVector.Matches(EF.Functions.ToTsQuery("simple", tsQuery)));
        }

        if (!string.IsNullOrWhiteSpace(s.FirstName))
        {
            var p = Escape(s.FirstName.Trim()) + "%";
            q = q.Where(x => EF.Functions.ILike(x.FirstName, p));
        }
        if (!string.IsNullOrWhiteSpace(s.LastName))
        {
            var p = Escape(s.LastName.Trim()) + "%";
            q = q.Where(x => EF.Functions.ILike(x.LastName, p));
        }
        if (!string.IsNullOrWhiteSpace(s.Speciality))
        {
            var p = "%" + Escape(s.Speciality.Trim()) + "%";
            q = q.Where(x => x.Speciality != null && EF.Functions.ILike(x.Speciality, p));
        }
        if (!string.IsNullOrWhiteSpace(s.PlaceOfWork))
        {
            var p = "%" + Escape(s.PlaceOfWork.Trim()) + "%";
            q = q.Where(x => x.PlaceOfWork != null && EF.Functions.ILike(x.PlaceOfWork, p));
        }
        if (!string.IsNullOrWhiteSpace(s.Email))
        {
            var p = Escape(s.Email.Trim());
            q = q.Where(x => x.Email != null && EF.Functions.ILike(x.Email, p));
        }
        if (!string.IsNullOrWhiteSpace(s.Phone))
        {
            var phone = s.Phone.Trim();
            q = q.Where(x => x.Phone == phone);
        }
        if (s.IsActive is { } active)
            q = q.Where(x => x.IsActive == active);

        q = s.Sort switch
        {
            "-lastName" => q.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName).ThenBy(x => x.Id),
            "firstName" => q.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id),
            "-firstName" => q.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName).ThenBy(x => x.Id),
            "created" => q.OrderBy(x => x.Created).ThenBy(x => x.Id),
            "-created" => q.OrderByDescending(x => x.Created).ThenBy(x => x.Id),
            "updated" => q.OrderBy(x => x.Updated).ThenBy(x => x.Id),
            "-updated" => q.OrderByDescending(x => x.Updated).ThenBy(x => x.Id),
            _ => q.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
        };

        var total = await q.CountAsync(ct);
        var rows = await q.Skip(offset).Take(count).ToListAsync(ct);
        var result = new SearchResult(rows.Select(ToDto).ToList(), total);

        await cache.SetAsync(cacheKey, result, CacheTtl);
        return result;
    }

    #endregion

    #region UPDATE

    /// <summary>Полная замена данных врача. expectedVersion (из If-Match) включает проверку оптимистичной блокировки.</summary>
    public async Task<(UpdateStatus Status, PractitionerDto? Dto)> UpdateAsync(
        Guid id, PractitionerRequest r, int? expectedVersion, CancellationToken ct)
    {
        var e = await db.Practitioners.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return (UpdateStatus.NotFound, null);

        if (expectedVersion is { } v && v != e.Version)
            return (UpdateStatus.Conflict, ToDto(e));

        Apply(e, r);
        e.Version++;
        e.Updated = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // между чтением и записью запись изменил кто-то другой
            return (UpdateStatus.Conflict, null);
        }

        await cache.RemoveAsync(ItemKey(id));
        await cache.BumpListVersionAsync();
        return (UpdateStatus.Ok, ToDto(e));
    }
    
    #endregion

    #region DELETE
    
    /// <summary>Идемпотентное soft-удаление: повторный вызов не меняет состояние.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await db.Practitioners
            .Where(x => x.Id == id) // query filter уже исключает удалённых
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.Updated, now), ct);

        if (affected > 0)
        {
            await cache.RemoveAsync(ItemKey(id));
            await cache.BumpListVersionAsync();
        }
    }

    #endregion

    #region IMPORT

    /// <summary>
    /// Применяет событие импорта. Возвращает false, если событие уже обрабатывалось (дубль).
    /// Запись в processed_events и изменение данных — в одной транзакции.
    /// </summary>
    public async Task<bool> ApplyImportAsync(ImportEvent evt, CancellationToken ct)
    {
        var p = evt.Payload ?? throw new InvalidDataException("payload is missing");
        if (string.IsNullOrWhiteSpace(evt.EventId)) throw new InvalidDataException("eventId is missing");

        var isDelete = string.Equals(evt.Type, "delete", StringComparison.OrdinalIgnoreCase);
        var isUpsert = string.Equals(evt.Type, "upsert", StringComparison.OrdinalIgnoreCase);

        if (!isDelete && !isUpsert) throw new InvalidDataException($"unknown event type '{evt.Type}'");
        if (isDelete && p.Id is null) throw new InvalidDataException("payload.id is required for delete");
        if (isUpsert && (string.IsNullOrWhiteSpace(p.FirstName) || string.IsNullOrWhiteSpace(p.LastName)))
            throw new InvalidDataException("firstName and lastName are required for upsert");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.ProcessedEvents.Add(new ProcessedEvent { EventId = evt.EventId, ProcessedAt = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false; // дубль: событие уже было обработано (в т.ч. другим экземпляром consumer'а)
        }

        var now = DateTime.UtcNow;
        var id = p.Id ?? Guid.NewGuid();
        var e = await db.Practitioners.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);

        if (isDelete)
        {
            if (e is { IsDeleted: false })
            {
                e.IsDeleted = true;
                e.Version++;
                e.Updated = now;
                e.SourceEventId = evt.EventId;
            }
        }
        else if (e is null)
        {
            db.Practitioners.Add(new Practitioner
            {
                Id = id,
                FirstName = p.FirstName!.Trim(),
                LastName = p.LastName!.Trim(),
                Phone = Clean(p.Phone),
                Speciality = Clean(p.Speciality),
                PlaceOfWork = Clean(p.PlaceOfWork),
                Email = Clean(p.Email),
                IsActive = p.IsActive,
                Created = now,
                Updated = now,
                Version = 1,
                SourceEventId = evt.EventId
            });
        }
        else if (evt.OccurredAt is { } at && at.ToUniversalTime() < e.Updated)
        {
            // устаревшее событие (out-of-order) не должно затирать более свежие данные
            log.LogInformation("Событие {EventId} устарело относительно записи {Id} — данные не изменены", evt.EventId, id);
        }
        else
        {
            e.FirstName = p.FirstName!.Trim();
            e.LastName = p.LastName!.Trim();
            e.Phone = Clean(p.Phone);
            e.Speciality = Clean(p.Speciality);
            e.PlaceOfWork = Clean(p.PlaceOfWork);
            e.Email = Clean(p.Email);
            e.IsActive = p.IsActive;
            e.IsDeleted = false;
            e.Version++;
            e.Updated = now;
            e.SourceEventId = evt.EventId;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await cache.RemoveAsync(ItemKey(id));
        await cache.BumpListVersionAsync();
        return true;
    }

    #endregion

    #region helpers

    public static PractitionerDto ToDto(Practitioner e) => new(
        e.Id, e.FirstName, e.LastName, e.Phone, e.Speciality, e.PlaceOfWork, e.Email,
        e.IsActive, e.Created, e.Updated, e.Version);

    private static void Apply(Practitioner e, PractitionerRequest r)
    {
        e.FirstName = r.FirstName.Trim();
        e.LastName = r.LastName.Trim();
        e.Phone = Clean(r.Phone);
        e.Speciality = Clean(r.Speciality);
        e.PlaceOfWork = Clean(r.PlaceOfWork);
        e.Email = Clean(r.Email);
        e.IsActive = r.IsActive;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Экранирует спецсимволы LIKE (\ % _).</summary>
    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>"иван пет" → "иван:* &amp; пет:*" (только буквы/цифры, поэтому инъекции в tsquery невозможны).</summary>
    private static string? BuildTsQuery(string text)
    {
        var tokens = Regex.Matches(text, @"[\p{L}\p{N}]+").Select(m => m.Value).ToList();
        return tokens.Count == 0 ? null : string.Join(" & ", tokens.Select(t => t + ":*"));
    }

    private static string Hash(PractitionerSearch s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s, JsonDefaults.Options))));
    
    #endregion
}
