using Microsoft.EntityFrameworkCore;
using son.Models;

namespace son
{
    public class RepositoryContext : DbContext
    {
        public RepositoryContext(DbContextOptions<RepositoryContext> options)
            : base(options)
        {
        }

        //public DbSet<Building> Buildings { get; set; }
        //public DbSet<Parcel> Parcels { get; set; }
        public DbSet<RasterTable> RasterTables { get; set; }
      //  public DbSet<AlosClip> AlosClips { get; set; }  
        public DbSet<FaultLine> FaultLines { get; set; }
        public DbSet<Wetland> Wetlands { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
          

            modelBuilder.Entity<RasterTable>(entity =>
            {
                entity.ToTable("raster_table");
                entity.HasKey(e => e.Rid);
                entity.Property(e => e.Rid).HasColumnName("rid");
                entity.Property(e => e.Rast).HasColumnName("rast");
            });

            //modelBuilder.Entity<AlosClip>(entity =>
            //{
            //    entity.ToTable("alos_clip");
            //    entity.HasKey(e => e.Rid);
            //    entity.Property(e => e.Rid).HasColumnName("rid");
            //    entity.Property(e => e.Rast).HasColumnName("rast");
            //});

            modelBuilder.Entity<FaultLine>(entity =>
            {
                entity.ToTable("fay");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_0");
                entity.Property(e => e.Geom).HasColumnName("geom");
                entity.Property(e => e.FaultId).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("ad");
            });

            modelBuilder.Entity<Wetland>(entity =>
            {
                entity.ToTable("wetlands");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_0");
                entity.Property(e => e.Geom).HasColumnName("geom");
                entity.Property(e => e.WetlandId).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("ad");
            });


            modelBuilder.HasPostgresExtension("postgis");
            base.OnModelCreating(modelBuilder);
        }
    }
}
