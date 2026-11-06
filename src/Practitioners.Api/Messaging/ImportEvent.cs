namespace Practitioners.Api.Messaging;

public static class StreamNames
{
    public const string Import = "practitioners:import";
    public const string Group = "practitioners-consumers";
    public const string DeadLetter = "practitioners:import:dlq";
}

/// <summary>Событие импорта. EventId — ключ идемпотентности.</summary>
public class ImportEvent
{
    /// <summary>Уникальный id события. Если не передан при публикации через API — генерируется.</summary>
    public string? EventId { get; set; }

    /// <summary>upsert | delete</summary>
    public string Type { get; set; } = "upsert";

    /// <summary>Когда изменение произошло в системе-источнике. Более старые события не перетирают свежие данные.</summary>
    public DateTime? OccurredAt { get; set; }

    public ImportItem? Payload { get; set; }
}

public class ImportItem
{
    /// <summary>Идентификатор врача. Для upsert без Id создаётся новая запись.</summary>
    public Guid? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Speciality { get; set; }
    public string? PlaceOfWork { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
