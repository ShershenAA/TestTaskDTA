using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Practitioners.Api.Dtos;
using Practitioners.Api.Services;

namespace Practitioners.Api.Controllers;

/// <summary>
/// HL7 FHIR R5 (https://hl7.org/fhir/R5/http.html): read для ресурса Practitioner.
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
