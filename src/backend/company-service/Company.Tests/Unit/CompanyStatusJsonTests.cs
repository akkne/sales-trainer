using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Tests.Unit;

[TestFixture]
public sealed class CompanyStatusJsonTests
{
    private static readonly JsonSerializerOptions ApiJsonSerializerOptions = CreateApiJsonSerializerOptions();

    private static JsonSerializerOptions CreateApiJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Test]
    public void CompanyDetailDto_serializes_status_as_the_enum_member_name()
    {
        var now = DateTime.UtcNow;
        var dto = new CompanyDetailDto(Guid.NewGuid(), "Acme", "", CompanyStatus.Lead, 0, 0, 0, now, now);

        var json = JsonSerializer.Serialize(dto, ApiJsonSerializerOptions);

        json.Should().Contain("\"status\":\"Lead\"");
    }

    [Test]
    public void CompanySummaryDto_serializes_status_as_the_enum_member_name()
    {
        var now = DateTime.UtcNow;
        var dto = new CompanySummaryDto(Guid.NewGuid(), "Acme", "", CompanyStatus.DealWon, 0, 0, 0, now, now);

        var json = JsonSerializer.Serialize(dto, ApiJsonSerializerOptions);

        json.Should().Contain("\"status\":\"DealWon\"");
    }

    [Test]
    public void UpdateCompanyStatusRequestDto_deserializes_a_valid_status_string()
    {
        var request = JsonSerializer.Deserialize<UpdateCompanyStatusRequestDto>(
            "{\"status\":\"Contacted\"}", ApiJsonSerializerOptions);

        request.Should().NotBeNull();
        request!.Status.Should().Be(CompanyStatus.Contacted);
    }

    [Test]
    public void UpdateCompanyStatusRequestDto_deserialization_throws_for_an_unknown_status_string()
    {
        var deserialize = () => JsonSerializer.Deserialize<UpdateCompanyStatusRequestDto>(
            "{\"status\":\"Bogus\"}", ApiJsonSerializerOptions);

        deserialize.Should().Throw<JsonException>();
    }

    [Test]
    public void UpdateCompanyStatusRequestDto_with_a_missing_status_fails_required_validation()
    {
        var request = JsonSerializer.Deserialize<UpdateCompanyStatusRequestDto>("{}", ApiJsonSerializerOptions);
        request.Should().NotBeNull();
        request!.Status.Should().BeNull();

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        isValid.Should().BeFalse();
    }
}
