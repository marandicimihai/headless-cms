using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeadlessCms.Api.Migrations
{
    /// <inheritdoc />
    public partial class ResetDynamicContentToSingleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypes_ContentTypeVersions_WorkspaceId_ProjectId_Id_C~",
                table: "ContentTypes");

            migrationBuilder.DropTable(
                name: "ContentEntries");

            migrationBuilder.DropTable(
                name: "ContentFields");

            migrationBuilder.DropTable(
                name: "ContentTypeVersions");

            migrationBuilder.DropTable(
                name: "ContentTypes");

            migrationBuilder.CreateTable(
                name: "ContentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypes", x => x.Id);
                    table.UniqueConstraint(
                        "AK_ContentTypes_WorkspaceId_ProjectId_Id",
                        x => new { x.WorkspaceId, x.ProjectId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContentTypes_Projects_WorkspaceId_ProjectId",
                        columns: x => new { x.WorkspaceId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentTypes_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentEntries_ContentTypes_WorkspaceId_ProjectId_ContentTy~",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.ContentTypeId },
                        principalTable: "ContentTypes",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentEntries_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContentFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_ContentFields_ContentTypes_WorkspaceId_ProjectId_ContentTyp~",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.ContentTypeId },
                        principalTable: "ContentTypes",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_WorkspaceId_ProjectId_ContentTypeId_Status_C~",
                table: "ContentEntries",
                columns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentFields_WorkspaceId_ProjectId_ContentTypeId_Key",
                table: "ContentFields",
                columns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_WorkspaceId_ProjectId_Key",
                table: "ContentTypes",
                columns: new[] { "WorkspaceId", "ProjectId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentFields_ContentTypes_WorkspaceId_ProjectId_ContentTyp~",
                table: "ContentFields");

            migrationBuilder.RenameColumn(
                name: "ContentTypeId",
                table: "ContentFields",
                newName: "ContentTypeVersionId");

            migrationBuilder.RenameIndex(
                name: "IX_ContentFields_WorkspaceId_ProjectId_ContentTypeId_Key",
                table: "ContentFields",
                newName: "IX_ContentFields_WorkspaceId_ProjectId_ContentTypeVersionId_Key");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentVersionId",
                table: "ContentTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContentTypeVersionId",
                table: "ContentEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ContentTypeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypeVersions", x => x.Id);
                    table.UniqueConstraint("AK_ContentTypeVersions_WorkspaceId_ProjectId_ContentTypeId_Id", x => new { x.WorkspaceId, x.ProjectId, x.ContentTypeId, x.Id });
                    table.UniqueConstraint("AK_ContentTypeVersions_WorkspaceId_ProjectId_Id", x => new { x.WorkspaceId, x.ProjectId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContentTypeVersions_ContentTypes_WorkspaceId_ProjectId_Cont~",
                        columns: x => new { x.WorkspaceId, x.ProjectId, x.ContentTypeId },
                        principalTable: "ContentTypes",
                        principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_WorkspaceId_ProjectId_Id_CurrentVersionId",
                table: "ContentTypes",
                columns: new[] { "WorkspaceId", "ProjectId", "Id", "CurrentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_WorkspaceId_ProjectId_ContentTypeId_ContentT~",
                table: "ContentEntries",
                columns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "ContentTypeVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypeVersions_WorkspaceId_ProjectId_ContentTypeId_Ver~",
                table: "ContentTypeVersions",
                columns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentEntries_ContentTypeVersions_WorkspaceId_ProjectId_Co~",
                table: "ContentEntries",
                columns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "ContentTypeVersionId" },
                principalTable: "ContentTypeVersions",
                principalColumns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentFields_ContentTypeVersions_WorkspaceId_ProjectId_Con~",
                table: "ContentFields",
                columns: new[] { "WorkspaceId", "ProjectId", "ContentTypeVersionId" },
                principalTable: "ContentTypeVersions",
                principalColumns: new[] { "WorkspaceId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypes_ContentTypeVersions_WorkspaceId_ProjectId_Id_C~",
                table: "ContentTypes",
                columns: new[] { "WorkspaceId", "ProjectId", "Id", "CurrentVersionId" },
                principalTable: "ContentTypeVersions",
                principalColumns: new[] { "WorkspaceId", "ProjectId", "ContentTypeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
