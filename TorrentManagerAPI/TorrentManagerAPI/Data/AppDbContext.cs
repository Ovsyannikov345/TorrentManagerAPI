using Microsoft.EntityFrameworkCore;
using TorrentManagerAPI.Models;

namespace TorrentManagerAPI.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MovieDownload> MovieDownloads => Set<MovieDownload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovieDownload>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.TorrentFileName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.QBitTorrentHash)
                .HasMaxLength(64);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(e => e.Category)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QBitTorrentHash);
        });
    }
}
