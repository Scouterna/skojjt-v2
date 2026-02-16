using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Skojjt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "badge_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    parts_scout_short = table.Column<string[]>(type: "text[]", nullable: false),
                    parts_scout_long = table.Column<string[]>(type: "text[]", nullable: false),
                    parts_admin_short = table.Column<string[]>(type: "text[]", nullable: false),
                    parts_admin_long = table.Column<string[]>(type: "text[]", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badge_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    personal_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    mobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    alt_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mum_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    mum_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mum_mobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    dad_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    dad_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dad_mobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    street = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    zip_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    group_roles = table.Column<string>(type: "text", nullable: true),
                    member_years = table.Column<int[]>(type: "integer[]", nullable: false),
                    not_in_scoutnet = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    removed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scout_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    organisation_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    association_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    municipality_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "1480"),
                    api_key_waitinglist = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    api_key_all_members = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bank_account = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    default_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    signatory = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    signatory_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    signatory_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attendance_min_year = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    attendance_incl_hike = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    is_autumn = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semesters", x => x.id);
                    table.CheckConstraint("chk_semester_id", "id = (year * 10) + CASE WHEN is_autumn THEN 1 ELSE 0 END");
                });

            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scout_group_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    parts_scout_short = table.Column<string[]>(type: "text[]", nullable: false),
                    parts_scout_long = table.Column<string[]>(type: "text[]", nullable: false),
                    parts_admin_short = table.Column<string[]>(type: "text[]", nullable: false),
                    parts_admin_long = table.Column<string[]>(type: "text[]", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badges", x => x.id);
                    table.ForeignKey(
                        name: "FK_badges_scout_groups_scout_group_id",
                        column: x => x.scout_group_id,
                        principalTable: "scout_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scout_group_persons",
                columns: table => new
                {
                    person_id = table.Column<int>(type: "integer", nullable: false),
                    scout_group_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_group_persons", x => new { x.person_id, x.scout_group_id });
                    table.ForeignKey(
                        name: "FK_scout_group_persons_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scout_group_persons_scout_groups_scout_group_id",
                        column: x => x.scout_group_id,
                        principalTable: "scout_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "troops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scoutnet_id = table.Column<int>(type: "integer", nullable: false),
                    scout_group_id = table.Column<int>(type: "integer", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    default_start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false, defaultValue: new TimeOnly(18, 30, 0)),
                    default_duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_troops", x => x.id);
                    table.ForeignKey(
                        name: "FK_troops_scout_groups_scout_group_id",
                        column: x => x.scout_group_id,
                        principalTable: "scout_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_troops_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scout_group_id = table.Column<int>(type: "integer", nullable: true),
                    active_semester_id = table.Column<int>(type: "integer", nullable: true),
                    has_access = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_scout_groups_scout_group_id",
                        column: x => x.scout_group_id,
                        principalTable: "scout_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_users_semesters_active_semester_id",
                        column: x => x.active_semester_id,
                        principalTable: "semesters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "badge_parts_done",
                columns: table => new
                {
                    person_id = table.Column<int>(type: "integer", nullable: false),
                    badge_id = table.Column<int>(type: "integer", nullable: false),
                    part_index = table.Column<int>(type: "integer", nullable: false),
                    is_scout_part = table.Column<bool>(type: "boolean", nullable: false),
                    examiner_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    completed_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badge_parts_done", x => new { x.person_id, x.badge_id, x.part_index, x.is_scout_part });
                    table.ForeignKey(
                        name: "FK_badge_parts_done_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_badge_parts_done_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "badges_completed",
                columns: table => new
                {
                    person_id = table.Column<int>(type: "integer", nullable: false),
                    badge_id = table.Column<int>(type: "integer", nullable: false),
                    examiner = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    completed_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badges_completed", x => new { x.person_id, x.badge_id });
                    table.ForeignKey(
                        name: "FK_badges_completed_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_badges_completed_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    troop_id = table.Column<int>(type: "integer", nullable: false),
                    meeting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false, defaultValue: new TimeOnly(18, 30, 0)),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    is_hike = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meetings", x => x.id);
                    table.ForeignKey(
                        name: "FK_meetings_troops_troop_id",
                        column: x => x.troop_id,
                        principalTable: "troops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "troop_badges",
                columns: table => new
                {
                    troop_id = table.Column<int>(type: "integer", nullable: false),
                    badge_id = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_troop_badges", x => new { x.troop_id, x.badge_id });
                    table.ForeignKey(
                        name: "FK_troop_badges_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_troop_badges_troops_troop_id",
                        column: x => x.troop_id,
                        principalTable: "troops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "troop_persons",
                columns: table => new
                {
                    troop_id = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<int>(type: "integer", nullable: false),
                    is_leader = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    patrol = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_troop_persons", x => new { x.troop_id, x.person_id });
                    table.ForeignKey(
                        name: "FK_troop_persons_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_troop_persons_troops_troop_id",
                        column: x => x.troop_id,
                        principalTable: "troops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_attendances",
                columns: table => new
                {
                    meeting_id = table.Column<int>(type: "int", nullable: false),
                    person_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_attendances", x => new { x.meeting_id, x.person_id });
                    table.ForeignKey(
                        name: "FK_meeting_attendances_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meeting_attendances_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_badge_parts_done_badge",
                table: "badge_parts_done",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "IX_badge_templates_name",
                table: "badge_templates",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_badges_scout_group",
                table: "badges",
                column: "scout_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_badges_scout_group_id_name",
                table: "badges",
                columns: new[] { "scout_group_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_badges_completed_badge_id",
                table: "badges_completed",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "idx_meeting_attendances_person",
                table: "meeting_attendances",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "idx_meetings_troop_date",
                table: "meetings",
                columns: new[] { "troop_id", "meeting_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_scout_group_persons_group",
                table: "scout_group_persons",
                column: "scout_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_troop_badges_badge_id",
                table: "troop_badges",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "idx_troop_persons_person",
                table: "troop_persons",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "idx_troops_natural_key",
                table: "troops",
                columns: new[] { "scoutnet_id", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_troops_scout_group_semester",
                table: "troops",
                columns: new[] { "scout_group_id", "semester_id" });

            migrationBuilder.CreateIndex(
                name: "IX_troops_semester_id",
                table: "troops",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_active_semester_id",
                table: "users",
                column: "active_semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_scout_group_id",
                table: "users",
                column: "scout_group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "badge_parts_done");

            migrationBuilder.DropTable(
                name: "badge_templates");

            migrationBuilder.DropTable(
                name: "badges_completed");

            migrationBuilder.DropTable(
                name: "meeting_attendances");

            migrationBuilder.DropTable(
                name: "scout_group_persons");

            migrationBuilder.DropTable(
                name: "troop_badges");

            migrationBuilder.DropTable(
                name: "troop_persons");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "meetings");

            migrationBuilder.DropTable(
                name: "badges");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.DropTable(
                name: "troops");

            migrationBuilder.DropTable(
                name: "scout_groups");

            migrationBuilder.DropTable(
                name: "semesters");
        }
    }
}
