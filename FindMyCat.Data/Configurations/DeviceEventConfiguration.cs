using FindMyCat.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyCat.Data.Configurations;

public class DeviceEventConfiguration : IEntityTypeConfiguration<DeviceEvent>
{
    public void Configure(EntityTypeBuilder<DeviceEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UniqueId).IsRequired();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();

        builder.HasIndex(e => new { e.UniqueId, e.Kind, e.Id });
    }
}
