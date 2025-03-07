using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class Email : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_AspNetUsers_KeeperId",
                schema: "Dev",
                table: "Campaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_AspNetUsers_PlayerId",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_PlayerId_CampaignId",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_KeeperId",
                schema: "Dev",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.AddColumn<string>(
                name: "PlayerEmail",
                schema: "Dev",
                table: "Characters",
                type: "character varying(256)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeeperEmail",
                schema: "Dev",
                table: "Campaigns",
                type: "character varying(256)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "Dev",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AspNetUsers_Email",
                schema: "Dev",
                table: "AspNetUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_PlayerEmail",
                schema: "Dev",
                table: "Characters",
                column: "PlayerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_PlayerEmail_CampaignId",
                schema: "Dev",
                table: "Characters",
                columns: new[] { "PlayerEmail", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_KeeperEmail",
                schema: "Dev",
                table: "Campaigns",
                column: "KeeperEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                schema: "Dev",
                table: "AspNetUsers",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_AspNetUsers_KeeperEmail",
                schema: "Dev",
                table: "Campaigns",
                column: "KeeperEmail",
                principalSchema: "Dev",
                principalTable: "AspNetUsers",
                principalColumn: "Email",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_AspNetUsers_PlayerEmail",
                schema: "Dev",
                table: "Characters",
                column: "PlayerEmail",
                principalSchema: "Dev",
                principalTable: "AspNetUsers",
                principalColumn: "Email",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_AspNetUsers_KeeperEmail",
                schema: "Dev",
                table: "Campaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_AspNetUsers_PlayerEmail",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_PlayerEmail",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_PlayerEmail_CampaignId",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_KeeperEmail",
                schema: "Dev",
                table: "Campaigns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AspNetUsers_Email",
                schema: "Dev",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Email",
                schema: "Dev",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PlayerEmail",
                schema: "Dev",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "KeeperEmail",
                schema: "Dev",
                table: "Campaigns");

            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                schema: "Dev",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "Dev",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_PlayerId_CampaignId",
                schema: "Dev",
                table: "Characters",
                columns: new[] { "PlayerId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_KeeperId",
                schema: "Dev",
                table: "Campaigns",
                column: "KeeperId");

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_AspNetUsers_KeeperId",
                schema: "Dev",
                table: "Campaigns",
                column: "KeeperId",
                principalSchema: "Dev",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_AspNetUsers_PlayerId",
                schema: "Dev",
                table: "Characters",
                column: "PlayerId",
                principalSchema: "Dev",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
