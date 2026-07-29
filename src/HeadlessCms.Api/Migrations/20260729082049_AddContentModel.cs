using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeadlessCms.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Projects_TenantId_Id",
                table: "Projects",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "ContentEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContentFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Nullable = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Settings = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypes", x => x.Id);
                    table.UniqueConstraint("AK_ContentTypes_TenantId_ProjectId_Id", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContentTypes_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTypeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypeVersions", x => x.Id);
                    table.UniqueConstraint("AK_ContentTypeVersions_TenantId_ProjectId_ContentTypeId_Id", x => new { x.TenantId, x.ProjectId, x.ContentTypeId, x.Id });
                    table.UniqueConstraint("AK_ContentTypeVersions_TenantId_ProjectId_Id", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContentTypeVersions_ContentTypes_TenantId_ProjectId_Content~",
                        columns: x => new { x.TenantId, x.ProjectId, x.ContentTypeId },
                        principalTable: "ContentTypes",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_TenantId_ProjectId_ContentTypeId_ContentType~",
                table: "ContentEntries",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeId", "ContentTypeVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_TenantId_ProjectId_ContentTypeId_Status_Crea~",
                table: "ContentEntries",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentFields_TenantId_ProjectId_ContentTypeVersionId_Key",
                table: "ContentFields",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_TenantId_ProjectId_Id_CurrentVersionId",
                table: "ContentTypes",
                columns: new[] { "TenantId", "ProjectId", "Id", "CurrentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_TenantId_ProjectId_Key",
                table: "ContentTypes",
                columns: new[] { "TenantId", "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypeVersions_TenantId_ProjectId_ContentTypeId_Version",
                table: "ContentTypeVersions",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentEntries_ContentTypeVersions_TenantId_ProjectId_Conte~",
                table: "ContentEntries",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeId", "ContentTypeVersionId" },
                principalTable: "ContentTypeVersions",
                principalColumns: new[] { "TenantId", "ProjectId", "ContentTypeId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentEntries_ContentTypes_TenantId_ProjectId_ContentTypeId",
                table: "ContentEntries",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeId" },
                principalTable: "ContentTypes",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentFields_ContentTypeVersions_TenantId_ProjectId_Conten~",
                table: "ContentFields",
                columns: new[] { "TenantId", "ProjectId", "ContentTypeVersionId" },
                principalTable: "ContentTypeVersions",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypes_ContentTypeVersions_TenantId_ProjectId_Id_Curr~",
                table: "ContentTypes",
                columns: new[] { "TenantId", "ProjectId", "Id", "CurrentVersionId" },
                principalTable: "ContentTypeVersions",
                principalColumns: new[] { "TenantId", "ProjectId", "ContentTypeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypes_ContentTypeVersions_TenantId_ProjectId_Id_Curr~",
                table: "ContentTypes");

            migrationBuilder.DropTable(
                name: "ContentEntries");

            migrationBuilder.DropTable(
                name: "ContentFields");

            migrationBuilder.DropTable(
                name: "ContentTypeVersions");

            migrationBuilder.DropTable(
                name: "ContentTypes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Projects_TenantId_Id",
                table: "Projects");
        }
    }
}
