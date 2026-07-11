using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies.Models;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// Guards the JSON wire contract between company-service and ai-service for persona generation.
/// company-service serializes the persona Difficulty enum as a string (e.g. "Medium"), so
/// ai-service must accept string enum values. Without a <see cref="JsonStringEnumConverter"/>
/// registered on AddControllers, System.Text.Json only binds numeric enums and [ApiController]
/// auto-returns 400 before the controller runs — the exact bug this test locks down.
/// </summary>
[TestFixture]
public class PersonaRequestWireContractTests
{
    // Mirrors the converter registered in Program.cs's AddControllers().AddJsonOptions(...).
    private static readonly JsonSerializerOptions AppJsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [TestCase("Easy", PersonaDifficulty.Easy)]
    [TestCase("Medium", PersonaDifficulty.Medium)]
    [TestCase("Hard", PersonaDifficulty.Hard)]
    public void GeneratePersonaRequestDto_DeserializesStringDifficulty(string wireValue, PersonaDifficulty expected)
    {
        // Exactly the shape company-service posts (Difficulty serialized via enum.ToString()).
        var json = $$"""
            {"companyDescription":"Описание","contactName":"Иван","contactPosition":"Закупщик","difficulty":"{{wireValue}}"}
            """;

        var request = JsonSerializer.Deserialize<GeneratePersonaRequestDto>(json, AppJsonOptions);

        request.Should().NotBeNull();
        request!.Difficulty.Should().Be(expected);
        request.CompanyDescription.Should().Be("Описание");
    }
}
