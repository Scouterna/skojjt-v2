using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScoutGroupPersonFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to scout_group_persons table
            migrationBuilder.AddColumn<bool>(
                name: "not_in_scoutnet",
                table: "scout_group_persons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "group_roles",
                table: "scout_group_persons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "scout_group_persons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            // Create index for not_in_scoutnet lookups
            migrationBuilder.CreateIndex(
                name: "idx_scout_group_persons_not_in_scoutnet",
                table: "scout_group_persons",
                column: "not_in_scoutnet");

            // Remove deprecated columns from persons table (if they exist)
            // Note: These columns may not exist if this is a fresh database
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='persons' AND column_name='not_in_scoutnet') THEN
                        ALTER TABLE persons DROP COLUMN not_in_scoutnet;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='persons' AND column_name='group_roles') THEN
                        ALTER TABLE persons DROP COLUMN group_roles;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove index
            migrationBuilder.DropIndex(
                name: "idx_scout_group_persons_not_in_scoutnet",
                table: "scout_group_persons");

            // Remove columns from scout_group_persons
            migrationBuilder.DropColumn(
                name: "not_in_scoutnet",
                table: "scout_group_persons");

            migrationBuilder.DropColumn(
                name: "group_roles",
                table: "scout_group_persons");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "scout_group_persons");

            // Restore columns to persons table
            migrationBuilder.AddColumn<bool>(
                name: "not_in_scoutnet",
                table: "persons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "group_roles",
                table: "persons",
                type: "text",
                nullable: true);
        }
    }
}
