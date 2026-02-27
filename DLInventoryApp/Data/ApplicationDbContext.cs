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
    public DbSet<ItemLike> ItemLikes => Set<ItemLike>();
    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();
    public DbSet<InventoryCustomIdElement> CustomIdElements => Set<InventoryCustomIdElement>();
    public DbSet<InventorySequence> InventorySequences => Set<InventorySequence>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Category>()
            .HasIndex(c => c.Name)
            .IsUnique();
        builder.Entity<Inventory>(entity =>
        {
            entity
            .HasOne(inv => inv.Owner)
            .WithMany()
            .HasForeignKey(inv => inv.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        });
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
            entity
            .HasOne(i => i.CreatedBy)
            .WithMany()
            .HasForeignKey(i => i.CreatedById)
            .OnDelete(DeleteBehavior.Cascade);
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
        builder.Entity<ItemLike>(entity =>
        {
            entity.HasKey(il => new { il.UserId, il.ItemId });
            entity.HasOne(il => il.Item)
              .WithMany(i => i.Likes)
              .HasForeignKey(il => il.ItemId)
              .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(il => il.User)
              .WithMany(u => u.LikedItems)
              .HasForeignKey(il => il.UserId)
              .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DiscussionPost>(entity =>
        {
            entity.HasOne(dp => dp.Inventory)
              .WithMany(inv => inv.DiscussionPosts)
              .HasForeignKey(dp => dp.InventoryId)
              .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(dp => dp.Author)
              .WithMany()
              .HasForeignKey(dp => dp.AuthorId)
              .OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<InventoryCustomIdElement>(entity =>
        {
            entity.HasOne(e => e.Inventory)
                .WithMany(inv => inv.CustomIdElements)
                .HasForeignKey(e => e.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.InventoryId, e.Order }).IsUnique();
        });
        builder.Entity<InventorySequence>(entity =>
            {
                entity.HasKey(s => s.InventoryId);
                entity.HasOne(s => s.Inventory)
                    .WithOne()
                    .HasForeignKey<InventorySequence>(s => s.InventoryId)
                    .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
