using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class PersonaControllerTests
{
    private IPersonaService _personaService = null!;
    private PersonaController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _personaService = Substitute.For<IPersonaService>();
        _controller = new PersonaController(_personaService, NullLogger<PersonaController>.Instance);
    }

    private static GeneratePersonaRequestDto BuildRequest(string companyDescription = "Описание компании") =>
        new(companyDescription, "Иван", "Закупщик", PersonaDifficulty.Medium);

    [Test]
    public async Task GeneratePersona_ReturnsOkWithPersonaFields()
    {
        _personaService
            .GeneratePersonaAsync(Arg.Any<GeneratePersonaRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedPersonaDto("Мария Соколова", "Руководитель закупок", "Прагматична."));

        var result = await _controller.GeneratePersona(BuildRequest());

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<GeneratedPersonaDto>().Subject;
        body.Name.Should().Be("Мария Соколова");
        body.Position.Should().Be("Руководитель закупок");
    }

    [Test]
    public async Task GeneratePersona_ReturnsBadRequest_WhenCompanyDescriptionExceedsLimit()
    {
        var request = BuildRequest(new string('a', 16001));

        var result = await _controller.GeneratePersona(request);

        result.Should().BeOfType<BadRequestObjectResult>();
        await _personaService.DidNotReceive()
            .GeneratePersonaAsync(Arg.Any<GeneratePersonaRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GeneratePersona_ReturnsServiceUnavailable_OnInvalidOperationException()
    {
        _personaService
            .GeneratePersonaAsync(Arg.Any<GeneratePersonaRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<GeneratedPersonaDto>(_ => throw new InvalidOperationException("unparseable"));

        var result = await _controller.GeneratePersona(BuildRequest());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task GeneratePersona_ReturnsServiceUnavailable_OnHttpRequestException()
    {
        _personaService
            .GeneratePersonaAsync(Arg.Any<GeneratePersonaRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<GeneratedPersonaDto>(_ => throw new HttpRequestException("provider error"));

        var result = await _controller.GeneratePersona(BuildRequest());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }
}
