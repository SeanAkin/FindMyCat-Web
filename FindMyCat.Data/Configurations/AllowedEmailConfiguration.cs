using FindMyCat.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FindMyCat.Data.Configurations;

public class AllowedEmailConfiguration : IEntityTypeConfiguration<AllowedEmail>
{
    public void Configure(EntityTypeBuilder<AllowedEmail> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email).IsRequired().HasMaxLength(320);

        builder.HasIndex(a => a.Email).IsUnique();
    }
}
