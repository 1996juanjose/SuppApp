using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuplementosApp.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRechazadoSeguimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Rechazado",
                table: "SeguimientoClientes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rechazado",
                table: "SeguimientoClientes");
        }
    }
}
