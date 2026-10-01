using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCmSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cm");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth0_sub = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "citext", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false, defaultValue: "Player"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('Player', 'Keeper', 'Admin')");
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.CheckConstraint("ck_audit_log_action", "action IN ('Created', 'Updated', 'Deleted')");
                    table.ForeignKey(
                        name: "fk_audit_log_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false, defaultValue: "Campaign"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Planning"),
                    era = table.Column<string>(type: "text", nullable: false, defaultValue: "Classic"),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaigns", x => x.id);
                    table.CheckConstraint("ck_campaigns_era", "era IN ('Classic', 'Modern')");
                    table.CheckConstraint("ck_campaigns_kind", "kind IN ('Campaign', 'OneShot')");
                    table.CheckConstraint("ck_campaigns_status", "status IN ('Planning', 'Active', 'OnHold', 'Completed')");
                    table.ForeignKey(
                        name: "fk_campaigns_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "creatures",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false, defaultValue: "Other"),
                    description = table.Column<string>(type: "text", nullable: true),
                    statblock = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    statblock_version = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creatures", x => x.id);
                    table.CheckConstraint("ck_creatures_type", "type IN ('Other', 'MythicMonsters', 'MythicGods', 'Monsters', 'Beast')");
                    table.ForeignKey(
                        name: "fk_creatures_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "files",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: true),
                    external_url = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    sha256 = table.Column<string>(type: "text", nullable: true),
                    original_name = table.Column<string>(type: "text", nullable: true),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_files", x => x.id);
                    table.CheckConstraint("ck_files_storage_or_url", "(storage_key IS NULL) <> (external_url IS NULL)");
                    table.ForeignKey(
                        name: "fk_files_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "keeper_applications",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    reviewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_keeper_applications", x => x.id);
                    table.CheckConstraint("ck_keeper_applications_message_length", "length(message) <= 1000");
                    table.CheckConstraint("ck_keeper_applications_status", "status IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_keeper_applications_users_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_keeper_applications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "occupations",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    skill_points_formula = table.Column<string>(type: "text", nullable: false),
                    credit_rating_min = table.Column<int>(type: "integer", nullable: false),
                    credit_rating_max = table.Column<int>(type: "integer", nullable: false),
                    eras = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{Classic,Modern}'"),
                    is_lovecraftian = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_occupations", x => x.id);
                    table.CheckConstraint("ck_occupations_credit_rating", "credit_rating_min BETWEEN 0 AND 99 AND credit_rating_max BETWEEN credit_rating_min AND 99");
                    table.CheckConstraint("ck_occupations_eras", "eras <@ ARRAY['Classic', 'Modern']");
                    table.CheckConstraint("ck_occupations_skill_points_formula", "skill_points_formula IN ('Edu4', 'Edu2Dex2', 'Edu2App2', 'Edu2Str2', 'Edu2Pow2', 'Edu2DexOrStr2', 'Edu2AppOrPow2', 'Edu2DexOrPow2', 'Edu2AppOrDexOrStr2')");
                    table.ForeignKey(
                        name: "fk_occupations_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scenarios",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    body_md = table.Column<string>(type: "text", nullable: true),
                    setting = table.Column<string>(type: "text", nullable: true),
                    era = table.Column<string>(type: "text", nullable: true),
                    setting_date = table.Column<string>(type: "text", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_scenario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenarios", x => x.id);
                    table.CheckConstraint("ck_scenarios_era", "era IN ('Classic', 'Modern')");
                    table.ForeignKey(
                        name: "fk_scenarios_scenarios_source_scenario_id",
                        column: x => x.source_scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_scenarios_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_value = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    base_formula = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    is_uncommon = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    eras = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{Classic,Modern}'"),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    usage_examples = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    failure_consequences = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    opposing_skills = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    time_required = table.Column<string>(type: "text", nullable: true),
                    can_retry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                    table.CheckConstraint("ck_skills_category", "category IN ('ProblemSolving', 'InformationGathering', 'Special', 'Social', 'Healing', 'CombatGeneral', 'Knowledge', 'CombatFirearms', 'Actions')");
                    table.CheckConstraint("ck_skills_eras", "eras <@ ARRAY['Classic', 'Modern']");
                    table.ForeignKey(
                        name: "fk_skills_skills_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "cm",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_skills_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "spells",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    alt_names = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    spell_type = table.Column<string>(type: "text", nullable: false),
                    cost = table.Column<string>(type: "text", nullable: true),
                    casting_time = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spells", x => x.id);
                    table.ForeignKey(
                        name: "fk_spells_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                schema: "cm",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_preferences", x => new { x.user_id, x.key });
                    table.ForeignKey(
                        name: "fk_user_preferences_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_members",
                schema: "cm",
                columns: table => new
                {
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaign_members", x => new { x.campaign_id, x.user_id });
                    table.CheckConstraint("ck_campaign_members_role", "role IN ('Player', 'Keeper')");
                    table.ForeignKey(
                        name: "fk_campaign_members_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "cm",
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaign_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "books",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    book_type = table.Column<string>(type: "text", nullable: false),
                    alt_names = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    language = table.Column<string>(type: "text", nullable: true),
                    year = table.Column<string>(type: "text", nullable: true),
                    author = table.Column<string>(type: "text", nullable: true),
                    sanity_loss = table.Column<string>(type: "text", nullable: true),
                    mythos_initial = table.Column<int>(type: "integer", nullable: true),
                    mythos_full = table.Column<int>(type: "integer", nullable: true),
                    mythos_rating = table.Column<int>(type: "integer", nullable: true),
                    study_weeks = table.Column<int>(type: "integer", nullable: true),
                    occultism_bonus = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    image_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_books", x => x.id);
                    table.CheckConstraint("ck_books_book_type", "book_type IN ('MythosBook', 'OccultBook')");
                    table.ForeignKey(
                        name: "fk_books_files_image_file_id",
                        column: x => x.image_file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_books_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "creature_images",
                schema: "cm",
                columns: table => new
                {
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_images", x => new { x.creature_id, x.ord });
                    table.ForeignKey(
                        name: "fk_creature_images_creatures_creature_id",
                        column: x => x.creature_id,
                        principalSchema: "cm",
                        principalTable: "creatures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_creature_images_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true),
                    eras = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{Classic,Modern}'"),
                    description = table.Column<string>(type: "text", nullable: true),
                    image_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.CheckConstraint("ck_items_eras", "eras <@ ARRAY['Classic', 'Modern']");
                    table.ForeignKey(
                        name: "fk_items_files_image_file_id",
                        column: x => x.image_file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_items_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "music_tracks",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    youtube_id = table.Column<string>(type: "text", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    loop = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    volume = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_music_tracks", x => x.id);
                    table.CheckConstraint("ck_music_tracks_volume", "volume BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_music_tracks_youtube_or_file", "(youtube_id IS NULL) <> (file_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_music_tracks_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_music_tracks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scenario_creatures",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    statblock = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    statblock_version = table.Column<int>(type: "integer", nullable: true),
                    count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    location_note = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_creatures", x => x.id);
                    table.CheckConstraint("ck_scenario_creatures_count", "count >= 1");
                    table.CheckConstraint("ck_scenario_creatures_source", "creature_id IS NOT NULL OR (name IS NOT NULL AND statblock IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_scenario_creatures_creatures_creature_id",
                        column: x => x.creature_id,
                        principalSchema: "cm",
                        principalTable: "creatures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scenario_creatures_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_handouts",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    player_text = table.Column<string>(type: "text", nullable: true),
                    keeper_note = table.Column<string>(type: "text", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_handouts", x => x.id);
                    table.ForeignKey(
                        name: "fk_scenario_handouts_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_scenario_handouts_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_key_facts",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_key_facts", x => x.id);
                    table.CheckConstraint("ck_scenario_key_facts_type", "type IN ('Backstory', 'Truth', 'Timeline', 'Reward')");
                    table.ForeignKey(
                        name: "fk_scenario_key_facts_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_locations",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    music_tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_scenario_locations_scenario_locations_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "cm",
                        principalTable: "scenario_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scenario_locations_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_runs",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Planned"),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    announcement = table.Column<string>(type: "text", nullable: true),
                    signup_open = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_runs", x => x.id);
                    table.CheckConstraint("ck_scenario_runs_status", "status IN ('Planned', 'Announced', 'Running', 'Finished')");
                    table.ForeignKey(
                        name: "fk_scenario_runs_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "cm",
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scenario_runs_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "occupation_slots",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occupation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    specialization = table.Column<string>(type: "text", nullable: true),
                    choose_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_occupation_slots", x => x.id);
                    table.CheckConstraint("ck_occupation_slots_choose_count", "choose_count >= 1");
                    table.CheckConstraint("ck_occupation_slots_kind", "kind IN ('Skill', 'Specialization', 'AnySpecialization', 'Choice', 'Social', 'Free')");
                    table.CheckConstraint("ck_occupation_slots_kind_fields", "(kind IN ('Skill', 'AnySpecialization') AND skill_id IS NOT NULL AND specialization IS NULL)\nOR (kind = 'Specialization' AND skill_id IS NOT NULL AND specialization IS NOT NULL)\nOR (kind IN ('Choice', 'Social', 'Free') AND skill_id IS NULL AND specialization IS NULL)");
                    table.ForeignKey(
                        name: "fk_occupation_slots_occupations_occupation_id",
                        column: x => x.occupation_id,
                        principalSchema: "cm",
                        principalTable: "occupations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_occupation_slots_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "cm",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weapons",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eras = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{Classic,Modern}'"),
                    is_rare = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_impaling = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    damage = table.Column<string>(type: "text", nullable: false),
                    damage_by_range = table.Column<string>(type: "jsonb", nullable: true),
                    range = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    base_range_m = table.Column<int>(type: "integer", nullable: true),
                    attacks = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    shots_per_round = table.Column<int>(type: "integer", nullable: true),
                    max_shots_per_round = table.Column<int>(type: "integer", nullable: true),
                    ammo = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ammo_capacity = table.Column<int>(type: "integer", nullable: true),
                    ammo_capacity_options = table.Column<int[]>(type: "integer[]", nullable: true),
                    single_use = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    malfunction = table.Column<int>(type: "integer", nullable: true),
                    cost = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    cost_classic = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_modern = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weapons", x => x.id);
                    table.CheckConstraint("ck_weapons_eras", "eras <@ ARRAY['Classic', 'Modern']");
                    table.CheckConstraint("ck_weapons_malfunction", "malfunction BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_weapons_type", "type IN ('Melee', 'Pistols', 'Rifles', 'Shotguns', 'AssaultRifles', 'SubmachineGuns', 'MachineGuns', 'ExplosivesAndHeavyWeapons', 'Other')");
                    table.ForeignKey(
                        name: "fk_weapons_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "cm",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_weapons_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    portrait_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sheet = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    sheet_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true, computedColumnSql: "sheet #>> '{personal,name}'", stored: true),
                    occupation = table.Column<string>(type: "text", nullable: true, computedColumnSql: "sheet #>> '{personal,occupation}'", stored: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characters", x => x.id);
                    table.CheckConstraint("ck_characters_kind", "kind IN ('Player', 'Pregen', 'Npc')");
                    table.CheckConstraint("ck_characters_owner", "(kind = 'Player' AND owner_id IS NOT NULL AND scenario_id IS NULL)\nOR (kind = 'Pregen' AND owner_id IS NULL AND campaign_id IS NULL)\nOR (kind = 'Npc' AND owner_id IS NULL AND scenario_id IS NULL)");
                    table.CheckConstraint("ck_characters_status", "status IN ('Active', 'Inactive', 'Retired', 'Archived')");
                    table.ForeignKey(
                        name: "fk_characters_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "cm",
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_characters_characters_origin_character_id",
                        column: x => x.origin_character_id,
                        principalSchema: "cm",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_characters_files_portrait_file_id",
                        column: x => x.portrait_file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_characters_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_characters_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_characters_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "book_spells",
                schema: "cm",
                columns: table => new
                {
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    raw_name = table.Column<string>(type: "text", nullable: false),
                    spell_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_spells", x => new { x.book_id, x.ord });
                    table.ForeignKey(
                        name: "fk_book_spells_books_book_id",
                        column: x => x.book_id,
                        principalSchema: "cm",
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_spells_spells_spell_id",
                        column: x => x.spell_id,
                        principalSchema: "cm",
                        principalTable: "spells",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scenario_items",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    location_note = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_items", x => x.id);
                    table.CheckConstraint("ck_scenario_items_source", "item_id IS NOT NULL OR name IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_scenario_items_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "cm",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scenario_items_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "location_tracks",
                schema: "cm",
                columns: table => new
                {
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_tracks", x => new { x.location_id, x.track_id });
                    table.ForeignKey(
                        name: "fk_location_tracks_music_tracks_track_id",
                        column: x => x.track_id,
                        principalSchema: "cm",
                        principalTable: "music_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_location_tracks_scenario_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "cm",
                        principalTable: "scenario_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_checks",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    target_kind = table.Column<string>(type: "text", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    characteristic = table.Column<string>(type: "text", nullable: true),
                    difficulty = table.Column<string>(type: "text", nullable: false, defaultValue: "Regular"),
                    on_success = table.Column<string>(type: "text", nullable: true),
                    on_failure = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_checks", x => x.id);
                    table.CheckConstraint("ck_scenario_checks_characteristic", "characteristic IN ('STR', 'CON', 'SIZ', 'DEX', 'APP', 'INT', 'POW', 'EDU')");
                    table.CheckConstraint("ck_scenario_checks_characteristic_target", "(target_kind = 'Characteristic') = (characteristic IS NOT NULL)");
                    table.CheckConstraint("ck_scenario_checks_difficulty", "difficulty IN ('Regular', 'Hard', 'Extreme')");
                    table.CheckConstraint("ck_scenario_checks_skill", "(target_kind = 'Skill') = (skill_id IS NOT NULL)");
                    table.CheckConstraint("ck_scenario_checks_target_kind", "target_kind IN ('Skill', 'Characteristic', 'Luck')");
                    table.ForeignKey(
                        name: "fk_scenario_checks_scenario_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "cm",
                        principalTable: "scenario_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scenario_checks_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "cm",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "campaign_sessions",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    number = table.Column<int>(type: "integer", nullable: false),
                    session_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    keeper_notes = table.Column<string>(type: "text", nullable: true),
                    scenario_completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaign_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaign_sessions_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "cm",
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaign_sessions_scenario_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "cm",
                        principalTable: "scenario_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "encounters",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    keeper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    state = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    state_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounters", x => x.id);
                    table.CheckConstraint("ck_encounters_kind", "kind IN ('Combat', 'Chase')");
                    table.CheckConstraint("ck_encounters_status", "status IN ('Active', 'Finished')");
                    table.ForeignKey(
                        name: "fk_encounters_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "cm",
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_encounters_scenario_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "cm",
                        principalTable: "scenario_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_encounters_users_keeper_id",
                        column: x => x.keeper_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "occupation_slot_options",
                schema: "cm",
                columns: table => new
                {
                    slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_occupation_slot_options", x => new { x.slot_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_occupation_slot_options_occupation_slots_slot_id",
                        column: x => x.slot_id,
                        principalSchema: "cm",
                        principalTable: "occupation_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_occupation_slot_options_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "cm",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "run_reservations",
                schema: "cm",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pregen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_reservations", x => new { x.run_id, x.pregen_id });
                    table.ForeignKey(
                        name: "fk_run_reservations_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "cm",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_run_reservations_characters_pregen_id",
                        column: x => x.pregen_id,
                        principalSchema: "cm",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_run_reservations_scenario_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "cm",
                        principalTable: "scenario_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_run_reservations_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_npcs",
                schema: "cm",
                columns: table => new
                {
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false, defaultValue: "Neutral"),
                    count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenario_npcs", x => new { x.scenario_id, x.character_id });
                    table.CheckConstraint("ck_scenario_npcs_count", "count >= 1");
                    table.CheckConstraint("ck_scenario_npcs_role", "role IN ('Neutral', 'Enemy', 'Ally')");
                    table.ForeignKey(
                        name: "fk_scenario_npcs_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "cm",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scenario_npcs_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalSchema: "cm",
                        principalTable: "scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "audit_log_entity",
                schema: "cm",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor_id",
                schema: "cm",
                table: "audit_log",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_spells_spell_id",
                schema: "cm",
                table: "book_spells",
                column: "spell_id");

            migrationBuilder.CreateIndex(
                name: "books_name_trgm",
                schema: "cm",
                table: "books",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_books_code",
                schema: "cm",
                table: "books",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_created_by_id",
                schema: "cm",
                table: "books",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_image_file_id",
                schema: "cm",
                table: "books",
                column: "image_file_id");

            migrationBuilder.CreateIndex(
                name: "campaign_members_one_keeper",
                schema: "cm",
                table: "campaign_members",
                column: "campaign_id",
                unique: true,
                filter: "role = 'Keeper'");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_members_user_id",
                schema: "cm",
                table: "campaign_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "campaign_sessions_journal",
                schema: "cm",
                table: "campaign_sessions",
                columns: new[] { "campaign_id", "session_date", "number" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_sessions_run_id",
                schema: "cm",
                table: "campaign_sessions",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_created_by_id",
                schema: "cm",
                table: "campaigns",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "characters_campaign",
                schema: "cm",
                table: "characters",
                column: "campaign_id",
                filter: "campaign_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "characters_kind_status",
                schema: "cm",
                table: "characters",
                columns: new[] { "kind", "status" });

            migrationBuilder.CreateIndex(
                name: "characters_one_active_sheet",
                schema: "cm",
                table: "characters",
                columns: new[] { "campaign_id", "owner_id" },
                unique: true,
                filter: "kind = 'Player' AND status = 'Active' AND campaign_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "characters_owner",
                schema: "cm",
                table: "characters",
                column: "owner_id",
                filter: "owner_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "characters_scenario",
                schema: "cm",
                table: "characters",
                column: "scenario_id",
                filter: "scenario_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_characters_created_by_id",
                schema: "cm",
                table: "characters",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_characters_origin_character_id",
                schema: "cm",
                table: "characters",
                column: "origin_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_characters_portrait_file_id",
                schema: "cm",
                table: "characters",
                column: "portrait_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_creature_images_file_id",
                schema: "cm",
                table: "creature_images",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "creatures_name_trgm",
                schema: "cm",
                table: "creatures",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_creatures_code",
                schema: "cm",
                table: "creatures",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creatures_created_by_id",
                schema: "cm",
                table: "creatures",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "encounters_one_active",
                schema: "cm",
                table: "encounters",
                columns: new[] { "keeper_id", "kind", "campaign_id" },
                unique: true,
                filter: "status = 'Active'")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_encounters_campaign_id",
                schema: "cm",
                table: "encounters",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_run_id",
                schema: "cm",
                table: "encounters",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_files_storage_key",
                schema: "cm",
                table: "files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_files_uploaded_by_id",
                schema: "cm",
                table: "files",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "items_name_trgm",
                schema: "cm",
                table: "items",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_items_code",
                schema: "cm",
                table: "items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_items_created_by_id",
                schema: "cm",
                table: "items",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_image_file_id",
                schema: "cm",
                table: "items",
                column: "image_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_keeper_applications_reviewed_by_id",
                schema: "cm",
                table: "keeper_applications",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "keeper_applications_one_pending",
                schema: "cm",
                table: "keeper_applications",
                column: "user_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_location_tracks_track_id",
                schema: "cm",
                table: "location_tracks",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_music_tracks_created_by_id",
                schema: "cm",
                table: "music_tracks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_music_tracks_file_id",
                schema: "cm",
                table: "music_tracks",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "music_tracks_tags",
                schema: "cm",
                table: "music_tracks",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_occupation_slot_options_skill_id",
                schema: "cm",
                table: "occupation_slot_options",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_occupation_slots_occupation_id_ord",
                schema: "cm",
                table: "occupation_slots",
                columns: new[] { "occupation_id", "ord" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_occupation_slots_skill_id",
                schema: "cm",
                table: "occupation_slots",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_occupations_code",
                schema: "cm",
                table: "occupations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_occupations_created_by_id",
                schema: "cm",
                table: "occupations",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_reservations_character_id",
                schema: "cm",
                table: "run_reservations",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_reservations_pregen_id",
                schema: "cm",
                table: "run_reservations",
                column: "pregen_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_reservations_run_id_user_id",
                schema: "cm",
                table: "run_reservations",
                columns: new[] { "run_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_run_reservations_user_id",
                schema: "cm",
                table: "run_reservations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_checks_location_id",
                schema: "cm",
                table: "scenario_checks",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_checks_skill_id",
                schema: "cm",
                table: "scenario_checks",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_creatures_creature_id",
                schema: "cm",
                table: "scenario_creatures",
                column: "creature_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_creatures_scenario_id",
                schema: "cm",
                table: "scenario_creatures",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_handouts_file_id",
                schema: "cm",
                table: "scenario_handouts",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_handouts_scenario_id",
                schema: "cm",
                table: "scenario_handouts",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_items_item_id",
                schema: "cm",
                table: "scenario_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_items_scenario_id",
                schema: "cm",
                table: "scenario_items",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_key_facts_scenario_id",
                schema: "cm",
                table: "scenario_key_facts",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_locations_parent_id",
                schema: "cm",
                table: "scenario_locations",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "scenario_locations_order",
                schema: "cm",
                table: "scenario_locations",
                columns: new[] { "scenario_id", "ord" });

            migrationBuilder.CreateIndex(
                name: "ix_scenario_npcs_character_id",
                schema: "cm",
                table: "scenario_npcs",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenario_runs_scenario_id",
                schema: "cm",
                table: "scenario_runs",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "scenario_runs_campaign",
                schema: "cm",
                table: "scenario_runs",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "scenario_runs_open",
                schema: "cm",
                table: "scenario_runs",
                column: "scheduled_at",
                filter: "signup_open");

            migrationBuilder.CreateIndex(
                name: "ix_scenarios_author_id",
                schema: "cm",
                table: "scenarios",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenarios_source_scenario_id",
                schema: "cm",
                table: "scenarios",
                column: "source_scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_code",
                schema: "cm",
                table: "skills",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_created_by_id",
                schema: "cm",
                table: "skills",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_parent_id",
                schema: "cm",
                table: "skills",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "skills_name_trgm",
                schema: "cm",
                table: "skills",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_spells_code",
                schema: "cm",
                table: "spells",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_spells_created_by_id",
                schema: "cm",
                table: "spells",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "spells_alt_names",
                schema: "cm",
                table: "spells",
                column: "alt_names")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "spells_name_trgm",
                schema: "cm",
                table: "spells",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_auth0_sub",
                schema: "cm",
                table: "users",
                column: "auth0_sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "cm",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_weapons_code",
                schema: "cm",
                table: "weapons",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_weapons_created_by_id",
                schema: "cm",
                table: "weapons",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_weapons_skill_id",
                schema: "cm",
                table: "weapons",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "weapons_name_trgm",
                schema: "cm",
                table: "weapons",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            // Ниже — то, что EF Core выразить не может (docs/v2/SCHEMA.md, «DDL»).

            // Имя справочника уникально без учёта регистра: индекс по выражению lower(name).
            foreach (var table in new[] { "skills", "occupations", "weapons", "spells", "books", "items", "creatures", "music_tracks" })
            {
                migrationBuilder.Sql($"CREATE UNIQUE INDEX {table}_name ON cm.{table} (lower(name));");
            }

            // Игрок листа — участник его кампании. Удалили участника — лист остаётся у игрока без
            // кампании: обнуляется только campaign_id (PostgreSQL 15+). EF умеет лишь SET NULL всех
            // колонок ключа, а обнулённый owner_id нарушил бы ck_characters_owner.
            migrationBuilder.Sql("""
                ALTER TABLE cm.characters
                    ADD CONSTRAINT fk_characters_campaign_members_campaign_id_owner_id
                    FOREIGN KEY (campaign_id, owner_id) REFERENCES cm.campaign_members (campaign_id, user_id)
                    ON DELETE SET NULL (campaign_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "book_spells",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "campaign_sessions",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "creature_images",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "data_protection_keys",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "encounters",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "keeper_applications",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "location_tracks",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "occupation_slot_options",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "run_reservations",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_checks",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_creatures",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_handouts",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_items",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_key_facts",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_npcs",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "user_preferences",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "weapons",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "books",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "spells",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "music_tracks",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "occupation_slots",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_runs",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenario_locations",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "creatures",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "items",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "characters",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "occupations",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "campaign_members",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "files",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "scenarios",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "users",
                schema: "cm");
        }
    }
}
