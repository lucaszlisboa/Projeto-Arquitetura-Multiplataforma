using Dentist_service.DentistsServices.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dentist_service.DentistsServices.Infrastructure.Data;

/// <summary>
/// DbContext da camada de infraestrutura.
/// Configura o mapeamento da entidade DentistServices para o PostgreSQL.
/// </summary>
public class DentistServicesDbContext : DbContext
{
    public DentistServicesDbContext(DbContextOptions<DentistServicesDbContext> options)
        : base(options) { }

    public DbSet<DentistServices> DentistServices => Set<DentistServices>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DentistServices>(entity =>
        {
            entity.ToTable("dentist_services");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.DentistId)
                .HasColumnName("dentist_id")
                .IsRequired();

            entity.Property(e => e.Price)
                .HasColumnName("price")
                .IsRequired();

            entity.Property(e => e.Isperiodic)
                .HasColumnName("is_periodic")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Índice por dentista para consultas frequentes
            entity.HasIndex(e => e.DentistId)
                .HasDatabaseName("ix_dentist_services_dentist_id");
        });
    }
}
