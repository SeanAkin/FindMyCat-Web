using FindMyCat.Core.Entities;

namespace FindMyCat.Core.RepositoryContracts;

public interface IDeviceEventRepository
{
    Task AddAsync(DeviceEvent deviceEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceEvent>> GetLatestPerKindAsync(string uniqueId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(
        string uniqueId, long afterId, string? kind, int limit, CancellationToken cancellationToken = default);
}
