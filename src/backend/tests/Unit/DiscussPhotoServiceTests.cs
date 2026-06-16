using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Discuss.Models;
using SalesTrainer.Api.Features.Discuss.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Storage.Abstract;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class DiscussPhotoServiceTests
{
    private SalesTrainer.Api.Infrastructure.Data.AppDbContext _db = null!;
    private InMemoryObjectStorage _storage = null!;
    private DiscussService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = InMemoryDbContextFactory.Create();
        _storage = new InMemoryObjectStorage();
        _service = new DiscussService(_db, _storage, NullLogger<DiscussService>.Instance);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    // ─── Upload happy path ──────────────────────────────────────────────────

    [Test]
    public async Task UploadPhotosAsync_AuthorUploadesTwoValidPngs_ReturnSuccessAndPersistsBothWithCorrectOrder()
    {
        var author = await TestDbSeeder.SeedUserAsync(_db);
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, author.Id);

        var files = new[]
        {
            MakePngFile("photo-a.png", 1024),
            MakePngFile("photo-b.png", 2048),
        };

        var (status, photos) = await _service.UploadPhotosAsync(
            DiscussPhotoOwner.Thread, thread.Id, author.Id, files);

        status.Should().Be(DiscussPhotoUploadStatus.Success);
        photos.Should().HaveCount(2);
        photos[0].OrderIndex.Should().Be(0);
        photos[1].OrderIndex.Should().Be(1);

        var persistedCount = _db.DiscussPhotos
            .Count(photo => photo.OwnerId == thread.Id && photo.OwnerType == DiscussPhotoOwner.Thread);
        persistedCount.Should().Be(2);

        _storage.PutCallCount.Should().Be(2);
    }

    // ─── Max-10 enforcement ─────────────────────────────────────────────────

    [Test]
    public async Task UploadPhotosAsync_OwnerAlreadyHasNinePhotos_UploadingTwoReturnsValidationErrorAndPersistsNothing()
    {
        var author = await TestDbSeeder.SeedUserAsync(_db);
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, author.Id);

        for (var index = 0; index < 9; index++)
        {
            _db.DiscussPhotos.Add(new DiscussPhoto
            {
                Id = Guid.NewGuid(),
                OwnerType = DiscussPhotoOwner.Thread,
                OwnerId = thread.Id,
                ObjectKey = $"discuss/threads/{thread.Id}/photo-{index}.png",
                ContentType = "image/png",
                OrderIndex = index,
                SizeBytes = 1024,
            });
        }
        await _db.SaveChangesAsync();

        var files = new[]
        {
            MakePngFile("extra-a.png", 512),
            MakePngFile("extra-b.png", 512),
        };

        var (status, photos) = await _service.UploadPhotosAsync(
            DiscussPhotoOwner.Thread, thread.Id, author.Id, files);

        status.Should().Be(DiscussPhotoUploadStatus.ValidationError);
        photos.Should().BeEmpty();

        _db.DiscussPhotos.Count(photo => photo.OwnerId == thread.Id).Should().Be(9);
        _storage.PutCallCount.Should().Be(0);
    }

    // ─── Authorization ──────────────────────────────────────────────────────

    [Test]
    public async Task UploadPhotosAsync_NonAuthorUploads_ReturnsForbiddenAndPersistsNothing()
    {
        var author = await TestDbSeeder.SeedUserAsync(_db, email: "author@test.com");
        var other = await TestDbSeeder.SeedUserAsync(_db, email: "other@test.com");
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, author.Id);

        var files = new[] { MakePngFile("photo.png", 512) };

        var (status, photos) = await _service.UploadPhotosAsync(
            DiscussPhotoOwner.Thread, thread.Id, other.Id, files);

        status.Should().Be(DiscussPhotoUploadStatus.Forbidden);
        photos.Should().BeEmpty();
        _db.DiscussPhotos.Should().BeEmpty();
        _storage.PutCallCount.Should().Be(0);
    }

    // ─── Content validation (bad magic bytes) ───────────────────────────────

    [Test]
    public async Task UploadPhotosAsync_BatchContainsFileWithInvalidMagicBytes_ReturnsValidationErrorAndPersistsNothing()
    {
        var author = await TestDbSeeder.SeedUserAsync(_db);
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, author.Id);

        var validFile = MakePngFile("good.png", 512);
        var invalidFile = MakeBadFile("bad.png");

        var (status, photos) = await _service.UploadPhotosAsync(
            DiscussPhotoOwner.Thread, thread.Id, author.Id, new[] { validFile, invalidFile });

        status.Should().Be(DiscussPhotoUploadStatus.ValidationError);
        photos.Should().BeEmpty();
        _db.DiscussPhotos.Should().BeEmpty();
        _storage.PutCallCount.Should().Be(0);
    }

    // ─── Delete thread cascades photo rows ──────────────────────────────────

    [Test]
    public async Task DeleteThreadAsync_ThreadHasPhotos_RemovesAllPhotoRows()
    {
        var author = await TestDbSeeder.SeedUserAsync(_db);
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, author.Id);

        _db.DiscussPhotos.Add(new DiscussPhoto
        {
            Id = Guid.NewGuid(),
            OwnerType = DiscussPhotoOwner.Thread,
            OwnerId = thread.Id,
            ObjectKey = $"discuss/threads/{thread.Id}/photo-0.png",
            ContentType = "image/png",
            OrderIndex = 0,
            SizeBytes = 1024,
        });
        await _db.SaveChangesAsync();

        var deleted = await _service.DeleteThreadAsync(thread.Id);

        deleted.Should().BeTrue();
        _db.DiscussPhotos.Should().BeEmpty();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static DiscussPhotoUploadFile MakePngFile(string name, long sizeBytes)
    {
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        var content = new byte[Math.Max(pngHeader.Length, (int)sizeBytes)];
        pngHeader.CopyTo(content, 0);
        return new DiscussPhotoUploadFile(new MemoryStream(content), name, content.Length);
    }

    private static DiscussPhotoUploadFile MakeBadFile(string name)
    {
        var garbage = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        return new DiscussPhotoUploadFile(new MemoryStream(garbage), name, garbage.Length);
    }
}

// In-memory IObjectStorage test double — no mocking framework required.
internal sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, (byte[] Data, string ContentType)> _store = new();

    public int PutCallCount { get; private set; }

    public Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var data = ReadAllBytes(content);
        _store[key] = (data, contentType);
        PutCallCount++;
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Object not found: {key}");
        return Task.FromResult<Stream>(new MemoryStream(entry.Data));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
