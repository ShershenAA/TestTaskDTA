using Microsoft.EntityFrameworkCore;
using Practitioners.Api.Domain;

namespace Practitioners.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Practitioner>(e =>
        {
            e.ToTable("practitioners");
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Speciality).HasMaxLength(200);
            e.Property(x => x.PlaceOfWork).HasMaxLength(300);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.SourceEventId).HasMaxLength(200);

            // оптимистичная блокировка: UPDATE ... WHERE Id = @id AND Version = @original
            e.Property(x => x.Version).IsConcurrencyToken();

            // soft delete
            e.HasQueryFilter(x => !x.IsDeleted);

            // полнотекстовый поиск: generated column + GIN индекс
            e.HasGeneratedTsVectorColumn(x => x.SearchVector, "simple", x => new { x.FirstName, x.LastName })
                .HasIndex(x => x.SearchVector)
                .HasMethod("GIN");

            e.HasIndex(x => x.LastName);
            e.HasIndex(x => x.Speciality);
            e.HasIndex(x => x.IsActive);
        });

        b.Entity<ProcessedEvent>(e =>
        {
            e.ToTable("processed_events");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasMaxLength(200);
        });

        b.Entity<IdempotencyKey>(e =>
        {
            e.ToTable("idempotency_keys");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(200);
        });
    }
}
