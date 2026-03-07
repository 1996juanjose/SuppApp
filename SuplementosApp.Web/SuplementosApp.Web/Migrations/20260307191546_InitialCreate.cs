using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuplementosApp.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanalesVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanalesVenta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoriasGasto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasGasto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadosVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosVenta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetodosEnvio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodosEnvio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetodosPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodosPago", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReporteDiario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GastoPublicidadFB = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalMensajes = table.Column<int>(type: "int", nullable: false),
                    CostoPorMensaje = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalPedidos = table.Column<int>(type: "int", nullable: false),
                    FacturacionTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ROAS = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AOV = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CPA = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TasaCierre = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReporteDiario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeguimientoClientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductoInteres = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LlamadaRealizada = table.Column<bool>(type: "bit", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeguimientoClientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ComisionBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gastos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoriaGastoId = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gastos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gastos_CategoriasGasto_CategoriaGastoId",
                        column: x => x.CategoriaGastoId,
                        principalTable: "CategoriasGasto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReporteDiarioDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporteDiarioId = table.Column<int>(type: "int", nullable: false),
                    VendedorId = table.Column<int>(type: "int", nullable: false),
                    Facturado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Publicidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Mensajes = table.Column<int>(type: "int", nullable: false),
                    Pedidos = table.Column<int>(type: "int", nullable: false),
                    TasaCierre = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReporteDiarioDetalle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReporteDiarioDetalle_ReporteDiario_ReporteDiarioId",
                        column: x => x.ReporteDiarioId,
                        principalTable: "ReporteDiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReporteDiarioDetalle_Vendedores_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Vendedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NombreCliente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DireccionEnvio = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Celular = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DNI = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstadoVentaId = table.Column<int>(type: "int", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NroOrden = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Cod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoEnvio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MetodoEnvioId = table.Column<int>(type: "int", nullable: false),
                    Productos = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetodoPagoId = table.Column<int>(type: "int", nullable: false),
                    CanalVentaId = table.Column<int>(type: "int", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Pagado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VendedorId = table.Column<int>(type: "int", nullable: false),
                    Comision = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_CanalesVenta_CanalVentaId",
                        column: x => x.CanalVentaId,
                        principalTable: "CanalesVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_EstadosVenta_EstadoVentaId",
                        column: x => x.EstadoVentaId,
                        principalTable: "EstadosVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_MetodosEnvio_MetodoEnvioId",
                        column: x => x.MetodoEnvioId,
                        principalTable: "MetodosEnvio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_MetodosPago_MetodoPagoId",
                        column: x => x.MetodoPagoId,
                        principalTable: "MetodosPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_Vendedores_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Vendedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_CategoriaGastoId",
                table: "Gastos",
                column: "CategoriaGastoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReporteDiario_Fecha",
                table: "ReporteDiario",
                column: "Fecha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReporteDiarioDetalle_ReporteDiarioId",
                table: "ReporteDiarioDetalle",
                column: "ReporteDiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ReporteDiarioDetalle_VendedorId",
                table: "ReporteDiarioDetalle",
                column: "VendedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_CanalVentaId",
                table: "Ventas",
                column: "CanalVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_EstadoVentaId",
                table: "Ventas",
                column: "EstadoVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_MetodoEnvioId",
                table: "Ventas",
                column: "MetodoEnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_MetodoPagoId",
                table: "Ventas",
                column: "MetodoPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_VendedorId",
                table: "Ventas",
                column: "VendedorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gastos");

            migrationBuilder.DropTable(
                name: "ReporteDiarioDetalle");

            migrationBuilder.DropTable(
                name: "SeguimientoClientes");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "CategoriasGasto");

            migrationBuilder.DropTable(
                name: "ReporteDiario");

            migrationBuilder.DropTable(
                name: "CanalesVenta");

            migrationBuilder.DropTable(
                name: "EstadosVenta");

            migrationBuilder.DropTable(
                name: "MetodosEnvio");

            migrationBuilder.DropTable(
                name: "MetodosPago");

            migrationBuilder.DropTable(
                name: "Vendedores");
        }
    }
}
