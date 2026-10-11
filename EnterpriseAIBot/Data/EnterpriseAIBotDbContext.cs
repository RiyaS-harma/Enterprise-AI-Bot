using EnterpriseAIBot.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAIBot.Data;

public sealed class EnterpriseAIBotDbContext : IdentityDbContext<AppUser, AppRole, string>
{
    public EnterpriseAIBotDbContext(DbContextOptions<EnterpriseAIBotDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DocumentMetadata> DocumentMetadata => Set<DocumentMetadata>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model); // required for Identity tables

        model.Entity<Conversation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SessionId).HasMaxLength(128).IsRequired();
            e.Property(x => x.UserId).HasMaxLength(256);
            e.HasIndex(x => x.SessionId);
            e.HasMany(x => x.Messages)
             .WithOne(x => x.Conversation)
             .HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(16).IsRequired();
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.SourcesJson).HasColumnType("nvarchar(max)");
        });

        model.Entity<DocumentMetadata>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.DocumentId).IsUnique();
            e.Property(x => x.DocumentName).HasMaxLength(512).IsRequired();
            e.Property(x => x.BlobPath).HasMaxLength(1024);
            e.Property(x => x.Department).HasMaxLength(128);
            e.Property(x => x.DocumentType).HasMaxLength(128);
        });
    }
}

