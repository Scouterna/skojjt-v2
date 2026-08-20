using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <summary>
    /// Links badge_parts_done rows imported from v1 to their normalized badge_parts row.
    ///
    /// The v1 import never populated badge_part_id, so migrated completions were invisible in
    /// the progress grid (which filters on badge_part_id) and ticking such a part inserted a
    /// duplicate of the composite primary key (person_id, badge_id, part_index, is_scout_part),
    /// failing with "An error occurred while saving the entity changes."
    ///
    /// v1 numbered scout and admin parts separately from zero, so part_index is matched against
    /// a part's position among the parts of the same kind rather than against sort_order, whose
    /// values differ between import, the add-part dialog and template-created badges.
    /// </summary>
    /// <inheritdoc />
    public partial class LinkLegacyBadgePartsDone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id,
                           badge_id,
                           is_admin_part,
                           row_number() OVER (
                               PARTITION BY badge_id, is_admin_part
                               ORDER BY sort_order, id
                           ) - 1 AS legacy_part_index
                    FROM badge_parts
                    WHERE badge_id IS NOT NULL
                )
                UPDATE badge_parts_done d
                SET badge_part_id = r.id
                FROM ranked r
                WHERE d.badge_part_id IS NULL
                  AND r.badge_id = d.badge_id
                  AND r.is_admin_part = (NOT d.is_scout_part)
                  AND r.legacy_part_index = d.part_index;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration: the links are a repair, not a schema change, and dropping
            // them again would only reintroduce the fault. Intentionally a no-op.
        }
    }
}
