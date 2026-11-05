using System.ComponentModel.DataAnnotations;

namespace Practitioners.Api.Dtos;

/// <summary>Тело запросов POST / PUT.</summary>
public class PractitionerRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = "";

    [StringLength(32), Phone]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? Speciality { get; set; }

    [StringLength(300)]
    public string? PlaceOfWork { get; set; }

    [StringLength(200), EmailAddress]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
}

public record PractitionerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone,
    string? Speciality,
    string? PlaceOfWork,
    string? Email,
    bool IsActive,
    DateTime Created,
    DateTime Updated,
    int Version);

/// <summary>Query-параметры REST-поиска.</summary>
public class PractitionerSearchQuery
{
    /// <summary>Полнотекстовый поиск по firstName + lastName (префиксный).</summary>
    public string? Q { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Speciality { get; set; }
    public string? PlaceOfWork { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>lastName | firstName | created | updated, с префиксом "-" для убывания.</summary>
    [RegularExpression("^-?(lastName|firstName|created|updated)$")]
    public string Sort { get; set; } = "lastName";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Внутренний критерий поиска (общий для REST и FHIR).</summary>
public record PractitionerSearch
{
    public Guid? Id { get; init; }
    public string? Name { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Speciality { get; init; }
    public string? PlaceOfWork { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool? IsActive { get; init; }
    public string Sort { get; init; } = "lastName";
    public int Offset { get; init; }
    public int Count { get; init; } = 20;
}

public record SearchResult(IReadOnlyList<PractitionerDto> Items, int Total);
