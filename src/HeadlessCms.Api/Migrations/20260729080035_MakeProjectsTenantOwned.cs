using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeadlessCms.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeProjectsTenantOwned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_OwnerId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Projects"
                SET "TenantId" = gen_random_uuid();

                INSERT INTO "Tenants" ("Id", "Name", "CreatedAt")
                SELECT "TenantId", "Name", "CreatedAt"
                FROM "Projects";

                INSERT INTO "TenantMemberships" ("TenantId", "UserId", "Role", "JoinedAt")
                SELECT "TenantId", "OwnerId", 'Owner', "CreatedAt"
                FROM "Projects";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Projects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_TenantId",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Projects" AS project
                SET "OwnerId" = (
                    SELECT membership."UserId"
                    FROM "TenantMemberships" AS membership
                    WHERE membership."TenantId" = project."TenantId"
                    ORDER BY CASE membership."Role"
                        WHEN 'Owner' THEN 0
                        WHEN 'Editor' THEN 1
                        ELSE 2
                    END,
                    membership."JoinedAt"
                    LIMIT 1
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "Projects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_OwnerId",
                table: "Projects",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
