using Microsoft.EntityFrameworkCore;

namespace SimpleSFTPSyncCore
{
    public class SimpleSFTPSyncCoreContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=SimpleSFTPSyncCore.sqlite");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SyncFile>(entity =>
            {
                entity.HasIndex(e => e.RemotePath)
                    .HasDatabaseName("sqlite_autoindex_SyncFile_2")
                    .IsUnique();

                entity.Property(e => e.SyncFileId).HasColumnName("SyncFileID");

                entity.Property(e => e.DateDiscovered).IsRequired();

                entity.Property(e => e.RemoteDateModified).IsRequired();

                entity.Property(e => e.RemotePath).IsRequired();
            });
        }

        public virtual DbSet<SyncFile> SyncFile { get; set; }
    }
}