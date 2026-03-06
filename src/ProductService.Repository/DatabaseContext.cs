using Microsoft.EntityFrameworkCore;
using Core.Models;
using ProductService.Models;
using ProductService.Repository.Configurations;

namespace ProductService.Repository;

public class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

    public DbSet<ProductModel> Products => Set<ProductModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
    }

    // Auto-set CreatedOn / ModifiedOn on every save
    public override int SaveChanges()
    {
        UpdateAuditableProperties();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableProperties();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditableProperties()
    {
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOn = DateTime.UtcNow;
                    entry.Entity.ModifiedOn = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.Entity.ModifiedOn = DateTime.UtcNow;
                    break;
            }
        }
    }
}
