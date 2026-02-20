using DLInventoryApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    { }
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<ItemFieldValue> ItemFieldValues => Set<ItemFieldValue>();
    public DbSet<InventoryWriteAccess> InventoryWriteAccesses => Set<InventoryWriteAccess>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<InventoryTag> InventoryTags => Set<InventoryTag>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Category>()
            .HasIndex(c => c.Name)
            .IsUnique();
        builder.Entity<Item>(entity =>
        {
            entity
            .HasOne(i => i.Inventory)
            .WithMany(inv => inv.Items)
            .HasForeignKey(i => i.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);
            entity
            .HasIndex(i => new { i.InventoryId, i.CustomId })
            .IsUnique();
        });
        builder.Entity<CustomField>(entity =>
        {
            entity.HasOne(cf => cf.Inventory)
            .WithMany(inv => inv.CustomFields)
            .HasForeignKey(cf => cf.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);
            entity
            .HasIndex(cf => new { cf.InventoryId, cf.Name })
            .IsUnique();
        });
        builder.Entity<ItemFieldValue>(entity =>
        {
            entity.HasKey(fv => new { fv.ItemId, fv.CustomFieldId });
            entity.HasOne(fv => fv.Item)
            .WithMany(i => i.FieldValues)
            .HasForeignKey(fv => fv.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(fv => fv.CustomField)
            .WithMany(f => f.FieldValues)
            .HasForeignKey(fv => fv.CustomFieldId)
            .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<InventoryWriteAccess>(entity =>
        {
            entity.HasKey(ia => new { ia.InventoryId, ia.UserId });
            entity.HasOne(ia => ia.Inventory)
            .WithMany(ia => ia.WriteAccesses)
            .HasForeignKey(ia => ia.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ia => ia.User)
            .WithMany()
            .HasForeignKey(ia => ia.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Tag>(entity =>
        {
            entity.HasIndex(t => t.Name)
                .IsUnique();
        });
        builder.Entity<InventoryTag>(entity =>
        {
            entity.HasKey(it => new { it.InventoryId, it.TagId });
            entity.HasOne(it => it.Inventory)
                .WithMany(inv => inv.InventoryTags)
                .HasForeignKey(it => it.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(it => it.Tag)
                .WithMany(t => t.InventoryTags)
                .HasForeignKey(it => it.TagId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(it => it.TagId);
        });
    }
}
