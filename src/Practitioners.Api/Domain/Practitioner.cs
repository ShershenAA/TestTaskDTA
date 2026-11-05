using NpgsqlTypes;

namespace Practitioners.Api.Domain;

public class Practitioner
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Speciality { get; set; }
    public string? PlaceOfWork { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    // служебные поля
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
    /// <summary>Optimistic concurrency token + FHIR meta.versionId.</summary>
    public int Version { get; set; }
    /// <summary>Soft delete.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>Id последнего события импорта, изменившего запись.</summary>
    public string? SourceEventId { get; set; }

    /// <summary>Генерируется БД (tsvector по firstName + lastName) для полнотекстового поиска.</summary>
    public NpgsqlTsVector SearchVector { get; set; } = default!;
}

/// <summary>Журнал обработанных событий импорта — идемпотентность consumer'а.</summary>
public class ProcessedEvent
{
    public string EventId { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
}

/// <summary>Ключи идемпотентности для POST (заголовок Idempotency-Key).</summary>
public class IdempotencyKey
{
    public string Key { get; set; } = default!;
    public Guid PractitionerId { get; set; }
    public DateTime Created { get; set; }
}
