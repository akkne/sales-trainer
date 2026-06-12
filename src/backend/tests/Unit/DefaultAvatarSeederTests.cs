using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Avatars;
using SalesTrainer.Api.Infrastructure.Storage.Abstract;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class DefaultAvatarSeederTests
{
    private sealed class FakeObjectStorage : IObjectStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public int PutCount { get; private set; }

        public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _store[key] = ms.ToArray();
            PutCount++;
            return Task.CompletedTask;
        }

        public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var data))
                return Task.FromResult<Stream>(new MemoryStream(data));
            throw new KeyNotFoundException(key);
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.ContainsKey(key));

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    private static DefaultAvatarSeeder CreateSeeder(
        SalesTrainer.Api.Infrastructure.Data.AppDbContext db,
        IObjectStorage storage)
    {
        return new DefaultAvatarSeeder(
            db,
            storage,
            NullLogger<DefaultAvatarSeeder>.Instance);
    }

    /// <summary>
    /// Ensures the seed-asset files exist on disk; if not (e.g. CI without build output),
    /// we write minimal valid PNGs to the expected location so the seeder can find them.
    /// </summary>
    private static void EnsureSeedAssetsExist()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "SeedAssets");
        Directory.CreateDirectory(dir);

        // Minimal 1x1 PNG bytes (69 bytes, valid PNG header + IHDR + IDAT + IEND)
        static byte[] MakePng(byte r, byte g, byte b)
        {
            static byte[] Chunk(byte[] tag, byte[] data)
            {
                uint crc = 0xffffffff;
                foreach (var bt in tag) crc = Crc32Step(crc, bt);
                foreach (var bt in data) crc = Crc32Step(crc, bt);
                crc ^= 0xffffffff;
                var len = BitConverter.GetBytes(data.Length);
                var c = BitConverter.GetBytes(crc);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(len);
                    Array.Reverse(c);
                }
                return [.. len, .. tag, .. data, .. c];
            }

            static uint Crc32Step(uint crc, byte b)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
                return crc;
            }

            var sig = new byte[] { 0x89, 0x4e, 0x47, 0x50, 0x0d, 0x0a, 0x1a, 0x0a };
            var ihdrData = new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0 };
            var ihdr = Chunk([(byte)'I', (byte)'H', (byte)'D', (byte)'R'], ihdrData);
            // scanline: filter=0, r, g, b; zlib-compressed
            var raw = new byte[] { 0, r, g, b };
            var compressed = ZlibCompress(raw);
            var idat = Chunk([(byte)'I', (byte)'D', (byte)'A', (byte)'T'], compressed);
            var iend = Chunk([(byte)'I', (byte)'E', (byte)'N', (byte)'D'], []);
            return [.. sig, .. ihdr, .. idat, .. iend];
        }

        static byte[] ZlibCompress(byte[] data)
        {
            using var ms = new System.IO.MemoryStream();
            using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest))
                ds.Write(data, 0, data.Length);
            var deflated = ms.ToArray();
            // zlib header: 0x78 0x9c, then deflate data, then Adler-32
            uint s1 = 1, s2 = 0;
            foreach (var bt in data) { s1 = (s1 + bt) % 65521; s2 = (s2 + s1) % 65521; }
            var adler = new byte[] { (byte)(s2 >> 8), (byte)(s2 & 0xff), (byte)(s1 >> 8), (byte)(s1 & 0xff) };
            return [0x78, 0x9c, .. deflated, .. adler];
        }

        byte[][] colors = [
            [100, 149, 237], [60, 179, 113], [255, 165, 0],
            [220, 20, 60],   [147, 112, 219], [64, 224, 208]
        ];

        for (var i = 0; i < colors.Length; i++)
        {
            var path = Path.Combine(dir, $"avatar-{i:00}.png");
            if (!File.Exists(path))
                File.WriteAllBytes(path, MakePng(colors[i][0], colors[i][1], colors[i][2]));
        }
    }

    [Test]
    public async Task SeedAsync_FirstRun_InsertsNRowsAndNObjects()
    {
        EnsureSeedAssetsExist();
        var db = InMemoryDbContextFactory.Create();
        var storage = new FakeObjectStorage();
        var seeder = CreateSeeder(db, storage);

        await seeder.SeedAsync();

        var rows = await db.DefaultAvatars.CountAsync();
        rows.Should().Be(DefaultAvatarSeeder.DefaultAvatarCount);
        storage.PutCount.Should().Be(DefaultAvatarSeeder.DefaultAvatarCount);
    }

    [Test]
    public async Task SeedAsync_SecondRun_NoAdditionalRowsOrPuts()
    {
        EnsureSeedAssetsExist();
        var db = InMemoryDbContextFactory.Create();
        var storage = new FakeObjectStorage();
        var seeder = CreateSeeder(db, storage);

        await seeder.SeedAsync();
        var putCountAfterFirst = storage.PutCount;
        var rowCountAfterFirst = await db.DefaultAvatars.CountAsync();

        await seeder.SeedAsync();

        var rows = await db.DefaultAvatars.CountAsync();
        rows.Should().Be(rowCountAfterFirst);
        storage.PutCount.Should().Be(putCountAfterFirst, "second run must not re-upload existing objects");
    }
}
