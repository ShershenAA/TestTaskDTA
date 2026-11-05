using System.Text.Json;
using System.Text.Json.Serialization;
using Practitioners.Api.Dtos;

namespace Practitioners.Api.Fhir;

public record FhirMeta(string VersionId, DateTime LastUpdated);
public record FhirHumanName(string Use, string Family, List<string> Given, string Text);
public record FhirTelecom(string System, string Value, string Use);
public record FhirCodeableConcept(string Text);
public record FhirQualification(FhirCodeableConcept Code);
public record FhirExtension(string Url, string ValueString);

/// <summary>HL7 FHIR R5 Practitioner (подмножество полей).</summary>
public record FhirPractitioner(
    string Id,
    FhirMeta Meta,
    bool Active,
    List<FhirHumanName> Name,
    List<FhirTelecom>? Telecom,
    List<FhirQualification>? Qualification,
    List<FhirExtension>? Extension)
{
    [JsonPropertyOrder(-1)]
    public string ResourceType => "Practitioner";
}

public record FhirBundleLink(string Relation, string Url);
public record FhirSearchInfo(string Mode);
public record FhirBundleEntry(string FullUrl, FhirPractitioner Resource, FhirSearchInfo Search);

public record FhirBundle(DateTime Timestamp, int Total, List<FhirBundleLink> Link, List<FhirBundleEntry> Entry)
{
    [JsonPropertyOrder(-2)]
    public string ResourceType => "Bundle";

    [JsonPropertyOrder(-1)]
    public string Type => "searchset";
}

public record OperationOutcomeIssue(string Severity, string Code, string Diagnostics);

public record OperationOutcome(List<OperationOutcomeIssue> Issue)
{
    [JsonPropertyOrder(-1)]
    public string ResourceType => "OperationOutcome";
}

public static class Fhir
{
    public const string ContentType = "application/fhir+json";
    public const string PlaceOfWorkUrl = "http://example.org/fhir/StructureDefinition/practitioner-place-of-work";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static FhirPractitioner ToFhir(PractitionerDto d)
    {
        var telecom = new List<FhirTelecom>();
        if (!string.IsNullOrEmpty(d.Phone)) telecom.Add(new FhirTelecom("phone", d.Phone, "work"));
        if (!string.IsNullOrEmpty(d.Email)) telecom.Add(new FhirTelecom("email", d.Email, "work"));

        return new FhirPractitioner(
            Id: d.Id.ToString(),
            Meta: new FhirMeta(d.Version.ToString(), d.Updated),
            Active: d.IsActive,
            Name: [new FhirHumanName("official", d.LastName, [d.FirstName], $"{d.FirstName} {d.LastName}")],
            Telecom: telecom.Count > 0 ? telecom : null,
            Qualification: string.IsNullOrEmpty(d.Speciality)
                ? null
                : [new FhirQualification(new FhirCodeableConcept(d.Speciality))],
            // в базовом ресурсе Practitioner нет места работы (оно живёт в PractitionerRole) — отдаём extension'ом
            Extension: string.IsNullOrEmpty(d.PlaceOfWork)
                ? null
                : [new FhirExtension(PlaceOfWorkUrl, d.PlaceOfWork)]);
    }

    public static OperationOutcome Outcome(string severity, string code, string message) =>
        new([new OperationOutcomeIssue(severity, code, message)]);
}
