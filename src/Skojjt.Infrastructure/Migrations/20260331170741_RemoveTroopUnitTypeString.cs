using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTroopUnitTypeString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unit_type",
                table: "troops");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unit_type",
                table: "troops",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
