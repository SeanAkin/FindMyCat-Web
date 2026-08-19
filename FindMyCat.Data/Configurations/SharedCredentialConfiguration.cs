using FindMyCat.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyCat.Data.Configurations;

public class SharedCredentialConfiguration : IEntityTypeConfiguration<SharedCredential>
{
    public void Configure(EntityTypeBuilder<SharedCredential> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TraccarApiTokenProtected);
        builder.Property(c => c.HologramApiKeyProtected);
    }
}
