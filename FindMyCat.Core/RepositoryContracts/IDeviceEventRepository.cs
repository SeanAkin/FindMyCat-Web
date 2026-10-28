using FindMyCat.Core.Entities;

namespace FindMyCat.Core.RepositoryContracts;

public interface IDeviceEventRepository
{
    Task AddAsync(DeviceEvent deviceEvent, CancellationToken cancellationToken = default);
}
