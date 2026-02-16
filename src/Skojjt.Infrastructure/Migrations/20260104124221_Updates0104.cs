using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Updates0104 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "group_roles",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "not_in_scoutnet",
                table: "persons");

            migrationBuilder.AddColumn<string>(
                name: "group_roles",
                table: "scout_group_persons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "not_in_scoutnet",
                table: "scout_group_persons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "scout_group_persons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.CreateIndex(
                name: "idx_scout_group_persons_not_in_scoutnet",
                table: "scout_group_persons",
                column: "not_in_scoutnet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_scout_group_persons_not_in_scoutnet",
                table: "scout_group_persons");

            migrationBuilder.DropColumn(
                name: "group_roles",
                table: "scout_group_persons");

            migrationBuilder.DropColumn(
                name: "not_in_scoutnet",
                table: "scout_group_persons");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "scout_group_persons");

            migrationBuilder.AddColumn<string>(
                name: "group_roles",
                table: "persons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "not_in_scoutnet",
                table: "persons",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
