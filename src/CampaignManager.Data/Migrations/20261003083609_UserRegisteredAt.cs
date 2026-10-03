using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserRegisteredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "registered_at",
                schema: "cm",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Уже перенесённым: дата первой записи пользователя (место в кампании — для Хранителя это создание кампании,
            // заявка, сценарий). v1 этих записей хранил с их датами, перенос их сохранил; нет записей — остаётся пусто.
            migrationBuilder.Sql(
                """
                UPDATE cm.users u SET registered_at = r.first_at
                FROM (
                    SELECT user_id, MIN(at) AS first_at FROM (
                        SELECT user_id, joined_at AS at FROM cm.campaign_members
                        UNION ALL SELECT user_id, created_at FROM cm.keeper_applications
                        UNION ALL SELECT author_id, created_at FROM cm.scenarios WHERE author_id IS NOT NULL
                    ) x GROUP BY user_id
                ) r
                WHERE r.user_id = u.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "registered_at",
                schema: "cm",
                table: "users");
        }
    }
}
