using FindMyCat.Core.Entities;
using FindMyCat.Data;
using FindMyCat.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.UnitTests.Repositories;

public sealed class DeviceEventRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly DeviceEventRepository _sut;

    public DeviceEventRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new DeviceEventRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<DeviceEvent> AddAsync(string uniqueId, string kind)
    {
        var deviceEvent = new DeviceEvent
        {
            UniqueId = uniqueId,
            Kind = kind,
            Payload = "{}",
            ReceivedAt = DateTimeOffset.UtcNow
        };
        _db.DeviceEvents.Add(deviceEvent);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return deviceEvent;
    }

    [Fact]
    public async Task GetLatestPerKindAsync_returns_the_newest_event_of_each_kind()
    {
        await AddAsync("collar-1", "network");
        await AddAsync("collar-1", "command");
        var latestNetwork = await AddAsync("collar-1", "network");
        var latestCommand = await AddAsync("collar-1", "command");
        await AddAsync("collar-2", "network");

        var result = await _sut.GetLatestPerKindAsync("collar-1", TestContext.Current.CancellationToken);

        result.Select(e => e.Id).ShouldBe([latestCommand.Id, latestNetwork.Id]);
    }

    [Fact]
    public async Task GetEventsAsync_returns_newest_first()
    {
        var first = await AddAsync("collar-1", "command");
        var second = await AddAsync("collar-1", "network");
        var third = await AddAsync("collar-1", "command");

        var result = await _sut.GetEventsAsync("collar-1", afterId: 0, kind: null, limit: 20, TestContext.Current.CancellationToken);

        result.Select(e => e.Id).ShouldBe([third.Id, second.Id, first.Id]);
    }

    [Fact]
    public async Task GetEventsAsync_returns_only_events_after_the_cursor()
    {
        await AddAsync("collar-1", "command");
        var cursor = await AddAsync("collar-1", "command");
        var after = await AddAsync("collar-1", "command");

        var result = await _sut.GetEventsAsync("collar-1", afterId: cursor.Id, kind: null, limit: 20, TestContext.Current.CancellationToken);

        result.Select(e => e.Id).ShouldBe([after.Id]);
    }

    [Fact]
    public async Task GetEventsAsync_filters_by_kind()
    {
        await AddAsync("collar-1", "command");
        var network = await AddAsync("collar-1", "network");
        await AddAsync("collar-1", "command");

        var result = await _sut.GetEventsAsync("collar-1", afterId: 0, kind: "network", limit: 20, TestContext.Current.CancellationToken);

        result.Select(e => e.Id).ShouldBe([network.Id]);
    }

    [Fact]
    public async Task GetEventsAsync_respects_the_limit()
    {
        for (var i = 0; i < 5; i++)
        {
            await AddAsync("collar-1", "command");
        }

        var result = await _sut.GetEventsAsync("collar-1", afterId: 0, kind: null, limit: 2, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetEventsAsync_scopes_to_the_unique_id()
    {
        await AddAsync("collar-2", "command");
        var mine = await AddAsync("collar-1", "command");

        var result = await _sut.GetEventsAsync("collar-1", afterId: 0, kind: null, limit: 20, TestContext.Current.CancellationToken);

        result.Select(e => e.Id).ShouldBe([mine.Id]);
    }
}
