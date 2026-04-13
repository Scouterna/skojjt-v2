using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTroopTypeAndCampFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "camp_end_date",
                table: "troops",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "camp_start_date",
                table: "troops",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "scoutnet_project_id",
                table: "troops",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "troop_type",
                table: "troops",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "camp_end_date",
                table: "troops");

            migrationBuilder.DropColumn(
                name: "camp_start_date",
                table: "troops");

            migrationBuilder.DropColumn(
                name: "scoutnet_project_id",
                table: "troops");

            migrationBuilder.DropColumn(
                name: "troop_type",
                table: "troops");
        }
    }
}
