using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Tests.Unit;

[TestFixture]
public sealed class CompanyFollowUpJsonTests
{
    private static readonly JsonSerializerOptions ApiJsonSerializerOptions = CreateApiJsonSerializerOptions();

    private static JsonSerializerOptions CreateApiJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Test]
    public void CompanyDetailDto_serializes_next_action_fields()
    {
        var now = DateTime.UtcNow;
        var nextActionAt = now.AddDays(3);
        var dto = new CompanyDetailDto(
            Guid.NewGuid(), "Acme", "", CompanyStatus.Lead, 0, 0, 0,
            nextActionAt, "Call back", null, now, now);

        var json = JsonSerializer.Serialize(dto, ApiJsonSerializerOptions);

        json.Should().Contain("\"nextActionNote\":\"Call back\"");
        json.Should().Contain("\"nextActionAt\"");
        json.Should().Contain("\"followUpNotifiedAt\":null");
    }

    [Test]
    public void CompanyDetailDto_serializes_null_next_action_fields_as_null()
    {
        var now = DateTime.UtcNow;
        var dto = new CompanyDetailDto(
            Guid.NewGuid(), "Acme", "", CompanyStatus.Lead, 0, 0, 0,
            null, null, null, now, now);

        var json = JsonSerializer.Serialize(dto, ApiJsonSerializerOptions);

        json.Should().Contain("\"nextActionAt\":null");
        json.Should().Contain("\"nextActionNote\":null");
        json.Should().Contain("\"followUpNotifiedAt\":null");
    }

    [Test]
    public void CompanySummaryDto_serializes_next_action_at()
    {
        var now = DateTime.UtcNow;
        var nextActionAt = now.AddDays(1);
        var dto = new CompanySummaryDto(
            Guid.NewGuid(), "Acme", "", CompanyStatus.Lead, 0, 0, 0, nextActionAt, now, now);

        var json = JsonSerializer.Serialize(dto, ApiJsonSerializerOptions);

        json.Should().Contain("\"nextActionAt\"");
    }

    [Test]
    public void UpdateCompanyFollowUpRequestDto_deserializes_a_valid_payload()
    {
        var nextActionAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var json = $"{{\"nextActionAt\":\"{nextActionAt:O}\",\"nextActionNote\":\"Follow up\"}}";

        var request = JsonSerializer.Deserialize<UpdateCompanyFollowUpRequestDto>(json, ApiJsonSerializerOptions);

        request.Should().NotBeNull();
        request!.NextActionAt.Should().Be(nextActionAt);
        request.NextActionNote.Should().Be("Follow up");
    }

    [Test]
    public void UpdateCompanyFollowUpRequestDto_with_null_next_action_at_deserializes_to_null()
    {
        var request = JsonSerializer.Deserialize<UpdateCompanyFollowUpRequestDto>(
            "{\"nextActionAt\":null,\"nextActionNote\":null}", ApiJsonSerializerOptions);

        request.Should().NotBeNull();
        request!.NextActionAt.Should().BeNull();
        request.NextActionNote.Should().BeNull();
    }
}
