using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;
using Microsoft.EntityFrameworkCore;

namespace FindMyCat.Data.Repositories;

internal sealed class DeviceEventRepository(AppDbContext db) : IDeviceEventRepository
{
    public async Task AddAsync(DeviceEvent deviceEvent, CancellationToken cancellationToken = default)
    {
        db.DeviceEvents.Add(deviceEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceEvent>> GetLatestPerKindAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        var latestIds = await db.DeviceEvents.AsNoTracking()
            .Where(e => e.UniqueId == uniqueId)
            .GroupBy(e => e.Kind)
            .Select(group => group.Max(e => e.Id))
            .ToListAsync(cancellationToken);

        return await db.DeviceEvents.AsNoTracking()
            .Where(e => latestIds.Contains(e.Id))
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(
        string uniqueId, long afterId, string? kind, int limit, CancellationToken cancellationToken = default)
    {
        var events = db.DeviceEvents.AsNoTracking()
            .Where(e => e.UniqueId == uniqueId && e.Id > afterId);

        if (!string.IsNullOrEmpty(kind))
        {
            events = events.Where(e => e.Kind == kind);
        }

        return await events
            .OrderByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
