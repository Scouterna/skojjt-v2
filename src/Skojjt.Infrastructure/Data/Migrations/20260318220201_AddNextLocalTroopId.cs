using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNextLocalTroopId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_troops_natural_key",
                table: "troops");

            migrationBuilder.AddColumn<int>(
                name: "next_local_troop_id",
                table: "scout_groups",
                type: "integer",
                nullable: false,
                defaultValue: 250);

            migrationBuilder.CreateIndex(
                name: "idx_troops_natural_key",
                table: "troops",
                columns: new[] { "scoutnet_id", "scout_group_id", "semester_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_troops_natural_key",
                table: "troops");

            migrationBuilder.DropColumn(
                name: "next_local_troop_id",
                table: "scout_groups");

            migrationBuilder.CreateIndex(
                name: "idx_troops_natural_key",
                table: "troops",
                columns: new[] { "scoutnet_id", "semester_id" },
                unique: true);
        }
    }
}
