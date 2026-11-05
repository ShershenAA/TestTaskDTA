using Microsoft.EntityFrameworkCore;
using Practitioners.Api.Data;
using Practitioners.Api.Domain;
using Practitioners.Api.Dtos;

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

    #region helpers

    public static PractitionerDto ToDto(Practitioner e) => new(
        e.Id, e.FirstName, e.LastName, e.Phone, e.Speciality, e.PlaceOfWork, e.Email,
        e.IsActive, e.Created, e.Updated, e.Version);

    #endregion
}
