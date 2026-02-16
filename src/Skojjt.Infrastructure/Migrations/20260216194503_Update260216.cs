using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update260216 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "default_location",
                table: "scout_groups",
                newName: "default_camp_location");

            migrationBuilder.AddColumn<string>(
                name: "default_meeting_location",
                table: "troops",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_meeting_location",
                table: "scout_groups",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_meeting_location",
                table: "troops");

            migrationBuilder.DropColumn(
                name: "default_meeting_location",
                table: "scout_groups");

            migrationBuilder.RenameColumn(
                name: "default_camp_location",
                table: "scout_groups",
                newName: "default_location");
        }
    }
}
