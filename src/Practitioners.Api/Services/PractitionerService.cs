using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Practitioners.Api.Data;
using Practitioners.Api.Domain;
using Practitioners.Api.Dtos;
using Practitioners.Api.Infrastructure;

namespace Practitioners.Api.Services;

public class PractitionerService(AppDbContext db, ICacheService cache)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static string ItemKey(Guid id) => $"prac:item:{id}";

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

    #region helpers

    public static PractitionerDto ToDto(Practitioner e) => new(
        e.Id, e.FirstName, e.LastName, e.Phone, e.Speciality, e.PlaceOfWork, e.Email,
        e.IsActive, e.Created, e.Updated, e.Version);

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
