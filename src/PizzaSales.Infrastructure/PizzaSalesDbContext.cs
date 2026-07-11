using Microsoft.EntityFrameworkCore;
using PizzaSales.Domain;

namespace PizzaSales.Infrastructure;

public sealed class PizzaSalesDbContext(DbContextOptions<PizzaSalesDbContext> options) : DbContext(options)
{
    public DbSet<PizzaType> PizzaTypes => Set<PizzaType>();
    public DbSet<Pizza> Pizzas => Set<Pizza>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PizzaType>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Category).HasMaxLength(64);
            entity.Property(x => x.Ingredients).HasMaxLength(1000);
        });

        modelBuilder.Entity<Pizza>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.PizzaTypeId).HasMaxLength(64);
            entity.Property(x => x.Size).HasMaxLength(4);
            entity.HasIndex(x => x.PizzaTypeId);
            entity.HasOne(x => x.PizzaType).WithMany(x => x.Pizzas).HasForeignKey(x => x.PizzaTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderDate);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.PizzaId);
            entity.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Pizza).WithMany(x => x.OrderItems).HasForeignKey(x => x.PizzaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
