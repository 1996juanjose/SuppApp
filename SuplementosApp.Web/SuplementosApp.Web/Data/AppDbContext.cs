using Microsoft.EntityFrameworkCore;

namespace SuplementosApp.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<Gasto> Gastos => Set<Gasto>();
    public DbSet<ReporteDiario> ReportesDiarios => Set<ReporteDiario>();
    public DbSet<ReporteDiarioDetalle> ReportesDiariosDetalle => Set<ReporteDiarioDetalle>();
    public DbSet<EstadoVenta> EstadosVenta => Set<EstadoVenta>();
    public DbSet<MetodoEnvio> MetodosEnvio => Set<MetodoEnvio>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<CanalVenta> CanalesVenta => Set<CanalVenta>();
    public DbSet<CategoriaGasto> CategoriasGasto => Set<CategoriaGasto>();
    public DbSet<SeguimientoCliente> SeguimientoClientes => Set<SeguimientoCliente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Venta
        modelBuilder.Entity<Venta>(entity =>
        {
            entity.ToTable("Ventas");
            entity.Property(v => v.NombreCliente).IsRequired().HasMaxLength(200);
            entity.Property(v => v.Region).HasMaxLength(100);
            entity.Property(v => v.DireccionEnvio).HasMaxLength(300);
            entity.Property(v => v.Celular).HasMaxLength(50);
            entity.Property(v => v.DNI).HasMaxLength(20);
            entity.Property(v => v.NroOrden).HasMaxLength(100);
            entity.Property(v => v.Cod).HasMaxLength(100);
            entity.Property(v => v.Clave).HasMaxLength(100);
            entity.Property(v => v.Productos).HasMaxLength(500);
            entity.Property(v => v.CostoEnvio).HasColumnType("decimal(18,2)");
            entity.Property(v => v.PrecioVenta).HasColumnType("decimal(18,2)");
            entity.Property(v => v.Pagado).HasColumnType("decimal(18,2)");
            entity.Property(v => v.Comision).HasColumnType("decimal(18,2)");
            entity.Property(v => v.TipoContacto).HasMaxLength(50).HasDefaultValue("Lead");

            entity.HasOne(v => v.EstadoVenta)
                .WithMany(e => e.Ventas)
                .HasForeignKey(v => v.EstadoVentaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.MetodoEnvio)
                .WithMany(m => m.Ventas)
                .HasForeignKey(v => v.MetodoEnvioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.MetodoPago)
                .WithMany(m => m.Ventas)
                .HasForeignKey(v => v.MetodoPagoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.CanalVenta)
                .WithMany(c => c.Ventas)
                .HasForeignKey(v => v.CanalVentaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Vendedor)
                .WithMany(ve => ve.Ventas)
                .HasForeignKey(v => v.VendedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Vendedor
        modelBuilder.Entity<Vendedor>(entity =>
        {
            entity.ToTable("Vendedores");
            entity.Property(v => v.Nombre).IsRequired().HasMaxLength(150);
            entity.Property(v => v.Email).HasMaxLength(200);
            entity.Property(v => v.ComisionBase).HasColumnType("decimal(18,2)");
        });

        // Gasto
        modelBuilder.Entity<Gasto>(entity =>
        {
            entity.ToTable("Gastos");
            entity.Property(g => g.Monto).HasColumnType("decimal(18,2)");

            entity.HasOne(g => g.CategoriaGasto)
                .WithMany(c => c.Gastos)
                .HasForeignKey(g => g.CategoriaGastoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ReporteDiario
        modelBuilder.Entity<ReporteDiario>(entity =>
        {
            entity.ToTable("ReporteDiario");
            entity.HasIndex(r => r.Fecha).IsUnique();
            entity.Property(r => r.GastoPublicidadFB).HasColumnType("decimal(18,2)");
            entity.Property(r => r.CostoPorMensaje).HasColumnType("decimal(18,4)");
            entity.Property(r => r.FacturacionTotal).HasColumnType("decimal(18,2)");
            entity.Property(r => r.ROAS).HasColumnType("decimal(18,4)");
            entity.Property(r => r.AOV).HasColumnType("decimal(18,4)");
            entity.Property(r => r.CPA).HasColumnType("decimal(18,4)");
            entity.Property(r => r.TasaCierre).HasColumnType("decimal(18,4)");
        });

        // ReporteDiarioDetalle
        modelBuilder.Entity<ReporteDiarioDetalle>(entity =>
        {
            entity.ToTable("ReporteDiarioDetalle");
            entity.Property(d => d.Facturado).HasColumnType("decimal(18,2)");
            entity.Property(d => d.Publicidad).HasColumnType("decimal(18,2)");
            entity.Property(d => d.TasaCierre).HasColumnType("decimal(18,4)");

            entity.HasOne(d => d.ReporteDiario)
                .WithMany(r => r.Detalles)
                .HasForeignKey(d => d.ReporteDiarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Vendedor)
                .WithMany(v => v.ReportesDiariosDetalle)
                .HasForeignKey(d => d.VendedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Catálogos
        modelBuilder.Entity<EstadoVenta>(entity =>
        {
            entity.ToTable("EstadosVenta");
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.HasData(
                new EstadoVenta { Id = 1, Nombre = "COMPROBANTE PROVINCIA" },
                new EstadoVenta { Id = 2, Nombre = "En destino" },
                new EstadoVenta { Id = 3, Nombre = "Entregado" },
                new EstadoVenta { Id = 4, Nombre = "Enviado" },
                new EstadoVenta { Id = 5, Nombre = "Lima embalado" },
                new EstadoVenta { Id = 6, Nombre = "Por enviar provincia" },
                new EstadoVenta { Id = 7, Nombre = "Cancelado/No quiere" }
            );
        });

        modelBuilder.Entity<MetodoEnvio>(entity =>
        {
            entity.ToTable("MetodosEnvio");
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.HasData(
                new MetodoEnvio { Id = 1, Nombre = "Shalom" },
                new MetodoEnvio { Id = 2, Nombre = "Olva Courier" }
            );
        });

        modelBuilder.Entity<MetodoPago>(entity =>
        {
            entity.ToTable("MetodosPago");
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.HasData(
                new MetodoPago { Id = 1, Nombre = "Yape" },
                new MetodoPago { Id = 2, Nombre = "BCP" },
                new MetodoPago { Id = 3, Nombre = "Efectivo" },
                new MetodoPago { Id = 4, Nombre = "Plin" },
                new MetodoPago { Id = 5, Nombre = "Mercado Pago" }
            );
        });

        modelBuilder.Entity<CanalVenta>(entity =>
        {
            entity.ToTable("CanalesVenta");
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.HasData(
                new CanalVenta { Id = 1, Nombre = "Orgánico" },
                new CanalVenta { Id = 2, Nombre = "Shopify" },
                new CanalVenta { Id = 3, Nombre = "WhatsApp" }
            );
        });

        modelBuilder.Entity<CategoriaGasto>(entity =>
        {
            entity.ToTable("CategoriasGasto");
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        });

        // SeguimientoCliente
        modelBuilder.Entity<SeguimientoCliente>(entity =>
        {
            entity.ToTable("SeguimientoClientes");
            entity.Property(c => c.Nombre).HasMaxLength(200);
            entity.Property(c => c.Numero).HasMaxLength(50);
            entity.Property(c => c.ProductoInteres).HasMaxLength(200);
        });
    }
}
