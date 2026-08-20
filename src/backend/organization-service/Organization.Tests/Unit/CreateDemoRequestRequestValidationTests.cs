using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DataAnnotationsRangeAttribute = System.ComponentModel.DataAnnotations.RangeAttribute;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Organization.Features.DemoRequests.Models;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Pins the two validation shapes on the public submission DTO that are easy to "tidy up" into
/// silent breakage.
///
/// <para>
/// <b>Why reflection over the constructor rather than <c>Validator.TryValidateObject</c>.</b> On a
/// positional record an attribute with no explicit target binds to the constructor <i>parameter</i>,
/// not to the generated property. ASP.NET Core reads those parameter attributes when it builds
/// <c>ModelMetadata</c>, so <c>[ApiController]</c> enforces them on a real request;
/// <c>System.ComponentModel</c>'s <c>Validator</c> only ever looks at properties and therefore reports
/// a fully invalid payload as valid. A test written against <c>Validator</c> would pass while proving
/// nothing about the endpoint, so the constructor is what gets inspected here.
/// </para>
/// </summary>
[TestFixture]
public sealed class CreateDemoRequestRequestValidationTests
{
    private static ParameterInfo Parameter(string parameterName) =>
        typeof(CreateDemoRequestRequestDto)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(parameter => parameter.Name == parameterName);

    /// <summary>
    /// Phone is required as of the 2026-08-20 owner decision: the sales motion this form feeds is
    /// phone-first, not because a reply technically needs it — <see cref="CreateDemoRequestRequestDto.WorkEmail"/>
    /// already covers that.
    /// </summary>
    [Test]
    public void The_phone_number_is_required()
    {
        var phone = Parameter(nameof(CreateDemoRequestRequestDto.Phone));

        phone.ParameterType.Should().Be(typeof(string));
        phone.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
    }

    /// <summary>
    /// <c>[Required]</c> on a <i>non-nullable</i> enum has nothing to reject: a body that omits the
    /// field binds to the zero member and <c>UpToFive</c> is stored as though a visitor had chosen the
    /// smallest bucket. Nullability is the whole mechanism, so it is asserted rather than assumed.
    /// </summary>
    [Test]
    public void The_sales_team_size_is_a_required_nullable_enum_so_omitting_it_is_a_400()
    {
        var salesTeamSize = Parameter(nameof(CreateDemoRequestRequestDto.SalesTeamSize));

        salesTeamSize.ParameterType.Should().Be(typeof(SalesTeamSize?));
        salesTeamSize.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
    }

    /// <summary>
    /// The data-processing consent must be ticked, which for a <c>bool</c> takes a <c>Range</c> pinned
    /// to <c>true</c> — <c>[Required]</c> alone accepts <c>false</c>.
    /// </summary>
    [Test]
    public void The_data_processing_consent_must_be_true_not_merely_present()
    {
        var consentGiven = Parameter(nameof(CreateDemoRequestRequestDto.ConsentGiven));

        consentGiven.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();

        var range = consentGiven.GetCustomAttribute<DataAnnotationsRangeAttribute>();
        range.Should().NotBeNull();
        range!.Minimum.Should().Be("true");
        range.Maximum.Should().Be("true");
    }

    /// <summary>
    /// The marketing consent is a separate, optional question (152-ФЗ/GDPR treat the two purposes
    /// separately). Any constraint here would force a visitor to accept marketing email to get a demo.
    /// </summary>
    [Test]
    public void The_marketing_consent_carries_no_constraint_because_declining_it_is_a_valid_answer()
    {
        var marketingConsent = Parameter(nameof(CreateDemoRequestRequestDto.MarketingConsentGiven));

        marketingConsent.ParameterType.Should().Be(typeof(bool));
        marketingConsent.GetCustomAttributes<ValidationAttribute>().Should().BeEmpty();
    }
}
