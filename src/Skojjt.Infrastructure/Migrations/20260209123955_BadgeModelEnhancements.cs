using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BadgeModelEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_scout_groups_scout_group_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_semesters_active_semester_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_active_semester_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_scout_group_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "accessible_group_ids",
                table: "users");

            migrationBuilder.DropColumn(
                name: "group_no",
                table: "users");

            migrationBuilder.DropColumn(
                name: "group_roles_json",
                table: "users");

            migrationBuilder.DropColumn(
                name: "has_access",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_member_registrar",
                table: "users");

            migrationBuilder.DropColumn(
                name: "scout_group_id",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "badges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "template_id",
                table: "badges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "badge_part_id",
                table: "badge_parts_done",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "undone_at",
                table: "badge_parts_done",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "badge_parts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    badge_id = table.Column<int>(type: "integer", nullable: true),
                    badge_template_id = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_admin_part = table.Column<bool>(type: "boolean", nullable: false),
                    short_description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    long_description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badge_parts", x => x.id);
                    table.CheckConstraint("ck_badge_parts_owner", "(badge_id IS NOT NULL AND badge_template_id IS NULL) OR (badge_id IS NULL AND badge_template_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_badge_parts_badge_templates_badge_template_id",
                        column: x => x.badge_template_id,
                        principalTable: "badge_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_badge_parts_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_badges_template",
                table: "badges",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "idx_badge_parts_done_badge_part",
                table: "badge_parts_done",
                column: "badge_part_id");

            migrationBuilder.CreateIndex(
                name: "idx_badge_parts_badge_sort",
                table: "badge_parts",
                columns: new[] { "badge_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "idx_badge_parts_template_sort",
                table: "badge_parts",
                columns: new[] { "badge_template_id", "sort_order" });

            migrationBuilder.AddForeignKey(
                name: "FK_badge_parts_done_badge_parts_badge_part_id",
                table: "badge_parts_done",
                column: "badge_part_id",
                principalTable: "badge_parts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_badges_badge_templates_template_id",
                table: "badges",
                column: "template_id",
                principalTable: "badge_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_badge_parts_done_badge_parts_badge_part_id",
                table: "badge_parts_done");

            migrationBuilder.DropForeignKey(
                name: "FK_badges_badge_templates_template_id",
                table: "badges");

            migrationBuilder.DropTable(
                name: "badge_parts");

            migrationBuilder.DropIndex(
                name: "idx_badges_template",
                table: "badges");

            migrationBuilder.DropIndex(
                name: "idx_badge_parts_done_badge_part",
                table: "badge_parts_done");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "badges");

            migrationBuilder.DropColumn(
                name: "template_id",
                table: "badges");

            migrationBuilder.DropColumn(
                name: "badge_part_id",
                table: "badge_parts_done");

            migrationBuilder.DropColumn(
                name: "undone_at",
                table: "badge_parts_done");

            migrationBuilder.AddColumn<string>(
                name: "accessible_group_ids",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_no",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_roles_json",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_access",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_member_registrar",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "scout_group_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_active_semester_id",
                table: "users",
                column: "active_semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_scout_group_id",
                table: "users",
                column: "scout_group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_scout_groups_scout_group_id",
                table: "users",
                column: "scout_group_id",
                principalTable: "scout_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_semesters_active_semester_id",
                table: "users",
                column: "active_semester_id",
                principalTable: "semesters",
                principalColumn: "id");
        }
    }
}
