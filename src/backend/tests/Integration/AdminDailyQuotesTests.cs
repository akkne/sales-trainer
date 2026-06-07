using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.DailyQuotes.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminDailyQuotesTests
{
    private static int _dateOffsetCounter;

    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private HttpClient _adminClient = null!;
    private HttpClient _userClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var admin = await TestDbSeeder.SeedUserAsync(_db,
            email: $"admin_{Guid.NewGuid()}@test.com", role: UserRole.Admin);
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"user_{Guid.NewGuid()}@test.com", role: UserRole.User);

        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);
        _userClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName, UserRole.User);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    // Each call returns a fresh date, so tests never clash on the unique Date index.
    private static DateOnly NextUniqueDate() =>
        new DateOnly(2030, 1, 1).AddDays(Interlocked.Increment(ref _dateOffsetCounter));

    private async Task<DailyQuote> SeedQuoteAsync(DateOnly? date = null)
    {
        var quote = new DailyQuote
        {
            Id = Guid.NewGuid(),
            Date = date ?? NextUniqueDate(),
            Text = $"Seed quote {Guid.NewGuid()}",
            Author = "Test Author",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.DailyQuotes.Add(quote);
        await _db.SaveChangesAsync();
        return quote;
    }

    [Test]
    public async Task GetAll_AsAdmin_FiltersByDateRange()
    {
        var quote = await SeedQuoteAsync();

        var response = await _adminClient.GetAsync(
            $"/admin/daily-quotes?from={quote.Date:yyyy-MM-dd}&to={quote.Date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        list.GetArrayLength().Should().Be(1);
        list[0].GetProperty("text").GetString().Should().Be(quote.Text);
    }

    [Test]
    public async Task GetAll_AsRegularUser_Returns403()
    {
        var response = await _userClient.GetAsync("/admin/daily-quotes");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_AsAdmin_Returns200WithCreatedQuote()
    {
        var date = NextUniqueDate();

        var response = await _adminClient.PostAsJsonAsync("/admin/daily-quotes", new
        {
            date = date.ToString("yyyy-MM-dd"),
            text = "Listen more than you talk.",
            author = "Coach"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("text").GetString().Should().Be("Listen more than you talk.");
        body.GetProperty("author").GetString().Should().Be("Coach");
        body.GetProperty("date").GetString().Should().Be(date.ToString("yyyy-MM-dd"));
    }

    [Test]
    public async Task Create_DuplicateDate_Returns409()
    {
        var existing = await SeedQuoteAsync();

        var response = await _adminClient.PostAsJsonAsync("/admin/daily-quotes", new
        {
            date = existing.Date.ToString("yyyy-MM-dd"),
            text = "Another quote",
            author = "Coach"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Create_EmptyText_Returns400()
    {
        var response = await _adminClient.PostAsJsonAsync("/admin/daily-quotes", new
        {
            date = NextUniqueDate().ToString("yyyy-MM-dd"),
            text = "  ",
            author = "Coach"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_AsAdmin_Returns200WithUpdatedData()
    {
        var quote = await SeedQuoteAsync();

        var response = await _adminClient.PutAsJsonAsync($"/admin/daily-quotes/{quote.Id}", new
        {
            date = quote.Date.ToString("yyyy-MM-dd"),
            text = "Updated text",
            author = "Updated Author"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("text").GetString().Should().Be("Updated text");
        body.GetProperty("author").GetString().Should().Be("Updated Author");
    }

    [Test]
    public async Task Update_NonExistentQuote_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync($"/admin/daily-quotes/{Guid.NewGuid()}", new
        {
            date = NextUniqueDate().ToString("yyyy-MM-dd"),
            text = "X",
            author = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_AsAdmin_Returns204()
    {
        var quote = await SeedQuoteAsync();

        var response = await _adminClient.DeleteAsync($"/admin/daily-quotes/{quote.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Delete_NonExistentQuote_Returns404()
    {
        var response = await _adminClient.DeleteAsync($"/admin/daily-quotes/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task PublicEndpoint_ReturnsQuoteForDate_AndFallsBackToEarlier()
    {
        var quote = await SeedQuoteAsync();

        var exactResponse = await _userClient.GetAsync($"/daily-quote?date={quote.Date:yyyy-MM-dd}");
        exactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var exactBody = await exactResponse.Content.ReadFromJsonAsync<JsonElement>();
        exactBody.GetProperty("text").GetString().Should().Be(quote.Text);

        // A later date with no dedicated quote falls back to the most recent earlier one.
        var fallbackDate = quote.Date.AddDays(100000);
        var fallbackResponse = await _userClient.GetAsync($"/daily-quote?date={fallbackDate:yyyy-MM-dd}");
        fallbackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
