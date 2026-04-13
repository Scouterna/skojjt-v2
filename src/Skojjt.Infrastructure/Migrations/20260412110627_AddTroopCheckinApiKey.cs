using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTroopCheckinApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scoutnet_checkin_api_key",
                table: "troops",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scoutnet_checkin_api_key",
                table: "troops");
        }
    }
}
