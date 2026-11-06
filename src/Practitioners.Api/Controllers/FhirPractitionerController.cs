using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Practitioners.Api.Dtos;
using Practitioners.Api.Services;

namespace Practitioners.Api.Controllers;

/// <summary>
/// HL7 FHIR R5 (https://hl7.org/fhir/R5/http.html): read и search для ресурса Practitioner.
/// </summary>
[ApiController]
[Route("fhir/Practitioner")]
[Produces(Fhir.Fhir.ContentType)]
public class FhirPractitionerController(PractitionerService service) : ControllerBase
{
    /// <summary>FHIR read: GET [base]/Practitioner/[id]</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Read(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            return Outcome(404, "not-found", $"Practitioner/{id} not found");

        var dto = await service.GetAsync(guid, ct);
        if (dto is null)
        {
            return await service.IsDeletedAsync(guid, ct)
                ? Outcome(410, "deleted", $"Practitioner/{id} was deleted")
                : Outcome(404, "not-found", $"Practitioner/{id} not found");
        }

        var etag = $"W/\"{dto.Version}\"";
        Response.Headers.ETag = etag;
        Response.Headers.LastModified = dto.Updated.ToUniversalTime().ToString("R");

        if (Request.Headers.IfNoneMatch == etag)
            return StatusCode(StatusCodes.Status304NotModified);

        return Resource(Fhir.Fhir.ToFhir(dto));
    }

    /// <summary>
    /// FHIR search: GET [base]/Practitioner?name=...&amp;family=...&amp;given=...&amp;active=true&amp;_count=20&amp;_offset=0&amp;_sort=family
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "_id")] string? resourceId,
        [FromQuery] string? name,
        [FromQuery] string? family,
        [FromQuery] string? given,
        [FromQuery] bool? active,
        [FromQuery] string? email,
        [FromQuery] string? phone,
        [FromQuery] string? speciality,
        [FromQuery(Name = "_count")] int count = 20,
        [FromQuery(Name = "_offset")] int offset = 0,
        [FromQuery(Name = "_sort")] string? sort = null,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 100);
        offset = Math.Max(offset, 0);

        var internalSort = sort switch
        {
            null or "" or "family" => "lastName",
            "-family" => "-lastName",
            "given" => "firstName",
            "-given" => "-firstName",
            "_lastUpdated" => "updated",
            "-_lastUpdated" => "-updated",
            _ => null
        };
        if (internalSort is null)
            return Outcome(400, "invalid", $"Unsupported _sort value '{sort}'. Use family, given, _lastUpdated (prefix '-' for descending).");

        Guid? guid = null;
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            if (!Guid.TryParse(resourceId, out var g))
                return Resource(BuildBundle([], 0, count, offset)); // невалидный id — пустой результат
            guid = g;
        }

        var result = await service.SearchAsync(new PractitionerSearch
        {
            Id = guid,
            Name = name,
            LastName = family,
            FirstName = given,
            IsActive = active,
            Email = email,
            Phone = phone,
            Speciality = speciality,
            Sort = internalSort,
            Offset = offset,
            Count = count
        }, ct);

        return Resource(BuildBundle(result.Items, result.Total, count, offset));
    }

    private Fhir.FhirBundle BuildBundle(IReadOnlyList<PractitionerDto> items, int total, int count, int offset)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/fhir/Practitioner";

        string Link(int off)
        {
            var pairs = Request.Query
                .Where(kv => kv.Key != "_offset" && kv.Key != "_count")
                .SelectMany(kv => kv.Value.Select(v => new KeyValuePair<string, string?>(kv.Key, v)))
                .Append(new KeyValuePair<string, string?>("_count", count.ToString()))
                .Append(new KeyValuePair<string, string?>("_offset", off.ToString()));
            return baseUrl + QueryString.Create(pairs);
        }

        var links = new List<Fhir.FhirBundleLink> { new("self", Link(offset)) };
        if (offset + count < total) links.Add(new("next", Link(offset + count)));
        if (offset > 0) links.Add(new("previous", Link(Math.Max(0, offset - count))));

        var entries = items
            .Select(d => new Fhir.FhirBundleEntry($"{baseUrl}/{d.Id}", Fhir.Fhir.ToFhir(d), new Fhir.FhirSearchInfo("match")))
            .ToList();

        return new Fhir.FhirBundle(DateTime.UtcNow, total, links, entries);
    }

    private ContentResult Resource(object resource) => new()
    {
        StatusCode = 200,
        ContentType = Fhir.Fhir.ContentType,
        Content = JsonSerializer.Serialize(resource, resource.GetType(), Fhir.Fhir.Json)
    };

    private static ContentResult Outcome(int status, string code, string message) => new()
    {
        StatusCode = status,
        ContentType = Fhir.Fhir.ContentType,
        Content = JsonSerializer.Serialize(Fhir.Fhir.Outcome("error", code, message), Fhir.Fhir.Json)
    };
}
