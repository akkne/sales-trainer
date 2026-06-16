using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Avatars.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Storage.Abstract;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

// ---------------------------------------------------------------------------
// In-memory IObjectStorage for avatar tests — avoids a real MinIO instance
// ---------------------------------------------------------------------------
internal sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, (byte[] Data, string ContentType)> _store = new();

    public Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _store[key] = (ms.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Object not found: {key}");

        Stream stream = new MemoryStream(entry.Data);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Avatar-specific factory that replaces IObjectStorage with the in-memory stub
// ---------------------------------------------------------------------------
internal sealed class AvatarTestWebApplicationFactory : TestWebApplicationFactory
{
    internal readonly InMemoryObjectStorage ObjectStorage = new();

    public AvatarTestWebApplicationFactory(string connectionString) : base(connectionString) { }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Apply base configuration (test DB, Hangfire swap, etc.)
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Remove the real S3 singleton and replace with in-memory stub
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IObjectStorage));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddSingleton<IObjectStorage>(ObjectStorage);
        });
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
[TestFixture]
public class AvatarsTests
{
    private AvatarTestWebApplicationFactory _factory = null!;
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;

    // A minimal valid 1×1 PNG (67 bytes)
    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk length + type
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // width=1, height=1
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // bit depth, color type, etc.
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
        0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
        0x44, 0xAE, 0x42, 0x60, 0x82
    };

    [SetUp]
    public void SetUp()
    {
        _factory = new AvatarTestWebApplicationFactory(IntegrationTestSetup.Postgres.GetConnectionString());
        // Run migrations so the test-specific factory has an up-to-date schema
        using var setupScope = _factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        setupDb.Database.Migrate();

        _scope = _factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [TearDown]
    public void TearDown()
    {
        _scope.Dispose();
        _factory.Dispose();
    }

    // -----------------------------------------------------------------------
    // POST /avatars — happy path
    // -----------------------------------------------------------------------
    [Test]
    public async Task UploadAvatar_ValidPng_Returns200AndSetsUploadedState()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_upload_{Guid.NewGuid()}@test.com");
        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "avatar.png");

        var response = await authedClient.PostAsync("/avatars", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var avatarUrl = body.GetProperty("avatarUrl").GetString();
        avatarUrl.Should().Be($"/avatars/{user.Id}");

        // DB state updated
        var loaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        loaded.AvatarType.Should().Be(AvatarKind.Uploaded);
        loaded.AvatarKey.Should().NotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // GET /avatars/{userId} — uploaded avatar round-trip
    // -----------------------------------------------------------------------
    [Test]
    public async Task GetAvatar_AfterUpload_ReturnsSameBytes()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_get_{Guid.NewGuid()}@test.com");
        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        // Upload first
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        uploadContent.Add(fileContent, "file", "avatar.png");
        var uploadResponse = await authedClient.PostAsync("/avatars", uploadContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then GET
        var getResponse = await authedClient.GetAsync($"/avatars/{user.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var returnedBytes = await getResponse.Content.ReadAsByteArrayAsync();
        returnedBytes.Should().Equal(MinimalPng);
    }

    // -----------------------------------------------------------------------
    // GET /avatars/{userId} — default avatar from seeded DefaultAvatars row
    // -----------------------------------------------------------------------
    [Test]
    public async Task GetAvatar_DefaultAvatarSeeded_Returns200WithDefaultBytes()
    {
        // The startup seeder already inserted DefaultAvatars rows for indices 0–5.
        // Look up an existing row so we never collide with the unique index on DefaultAvatar.Index.
        var existingDefault = await _db.DefaultAvatars
            .OrderBy(a => a.Index)
            .FirstAsync();

        // The test user defaults to DefaultAvatarIndex = 0, which matches existingDefault.Index (0).
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_default_{Guid.NewGuid()}@test.com");

        // Seed the in-memory object storage with fake bytes under the key the seeder registered.
        var defaultBytes = Encoding.UTF8.GetBytes("fake-default-png-bytes");
        await _factory.ObjectStorage.PutAsync(existingDefault.ObjectKey, new MemoryStream(defaultBytes), "image/png");

        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);
        var response = await authedClient.GetAsync($"/avatars/{user.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returnedBytes = await response.Content.ReadAsByteArrayAsync();
        returnedBytes.Should().Equal(defaultBytes);
    }

    // -----------------------------------------------------------------------
    // DELETE /avatars — resets to default
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAvatar_AfterUpload_ResetsToDefault()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_delete_{Guid.NewGuid()}@test.com");
        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        // Upload first
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        uploadContent.Add(fileContent, "file", "avatar.png");
        await authedClient.PostAsync("/avatars", uploadContent);

        // Then delete
        var deleteResponse = await authedClient.DeleteAsync("/avatars");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        loaded.AvatarType.Should().Be(AvatarKind.Default);
        loaded.AvatarKey.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // POST /avatars — disallowed extension → 400
    // -----------------------------------------------------------------------
    [Test]
    public async Task UploadAvatar_DisallowedExtension_Returns400()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_badext_{Guid.NewGuid()}@test.com");
        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("not an image"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "file", "malware.txt");

        var response = await authedClient.PostAsync("/avatars", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // POST /avatars — unauthenticated → 401
    // -----------------------------------------------------------------------
    [Test]
    public async Task UploadAvatar_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "avatar.png");

        var response = await anonClient.PostAsync("/avatars", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // POST /avatars — .png extension but non-image bytes → 400 (magic-byte check)
    // -----------------------------------------------------------------------
    [Test]
    public async Task UploadAvatar_PngExtensionButTextBytes_Returns400()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_magic_{Guid.NewGuid()}@test.com");
        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        // Bytes are plain text, not a PNG
        var fakeBytes = Encoding.UTF8.GetBytes("this is definitely not an image file");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fakeBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "avatar.png");

        var response = await authedClient.PostAsync("/avatars", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // GET /avatars/{userId} — response includes nosniff header and short cache
    // -----------------------------------------------------------------------
    [Test]
    public async Task GetAvatar_AfterUpload_HasNoSniffHeaderAndShortCache()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"av_headers_{Guid.NewGuid()}@test.com");
        var authedClient = _factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        // Upload first
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        uploadContent.Add(fileContent, "file", "avatar.png");
        var uploadResponse = await authedClient.PostAsync("/avatars", uploadContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET and check headers
        var getResponse = await authedClient.GetAsync($"/avatars/{user.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        getResponse.Headers.TryGetValues("X-Content-Type-Options", out var nosniffValues).Should().BeTrue();
        nosniffValues!.Should().Contain("nosniff");

        var cacheControl = getResponse.Headers.CacheControl;
        cacheControl.Should().NotBeNull();
        cacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
    }
}
