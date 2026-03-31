using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTroopUnitType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unit_type",
                table: "troops",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "unit_type_id",
                table: "troops",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unit_type",
                table: "troops");

            migrationBuilder.DropColumn(
                name: "unit_type_id",
                table: "troops");
        }
    }
}
