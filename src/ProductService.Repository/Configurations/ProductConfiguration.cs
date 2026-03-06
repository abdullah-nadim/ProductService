using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Models;

namespace ProductService.Repository.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<ProductModel>
{
    public void Configure(EntityTypeBuilder<ProductModel> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        builder.HasIndex(p => p.Sku).IsUnique();
    }
}
