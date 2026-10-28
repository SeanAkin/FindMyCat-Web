using FindMyCat.Core.Entities;
using FindMyCat.Core.RepositoryContracts;

namespace FindMyCat.Data.Repositories;

internal sealed class DeviceEventRepository(AppDbContext db) : IDeviceEventRepository
{
    public async Task AddAsync(DeviceEvent deviceEvent, CancellationToken cancellationToken = default)
    {
        db.DeviceEvents.Add(deviceEvent);
        await db.SaveChangesAsync(cancellationToken);
    }
}
