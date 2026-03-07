using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuplementosApp.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoContacto",
                table: "Ventas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Lead");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoContacto",
                table: "Ventas");
        }
    }
}
