using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SuplementosApp.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CanalesVenta",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "Orgánico" },
                    { 2, "Shopify" },
                    { 3, "WhatsApp" }
                });

            migrationBuilder.InsertData(
                table: "EstadosVenta",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "COMPROBANTE PROVINCIA" },
                    { 2, "En destino" },
                    { 3, "Entregado" },
                    { 4, "Enviado" },
                    { 5, "Lima embalado" },
                    { 6, "Por enviar provincia" },
                    { 7, "Cancelado/No quiere" }
                });

            migrationBuilder.InsertData(
                table: "MetodosEnvio",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "Shalom" },
                    { 2, "Olva Courier" }
                });

            migrationBuilder.InsertData(
                table: "MetodosPago",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "Yape" },
                    { 2, "BCP" },
                    { 3, "Efectivo" },
                    { 4, "Plin" },
                    { 5, "Mercado Pago" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CanalesVenta",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CanalesVenta",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CanalesVenta",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "EstadosVenta",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "MetodosEnvio",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MetodosEnvio",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MetodosPago",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MetodosPago",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MetodosPago",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "MetodosPago",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MetodosPago",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}
