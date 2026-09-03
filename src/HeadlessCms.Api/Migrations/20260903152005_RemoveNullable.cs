using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeadlessCms.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nullable",
                table: "ContentFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Nullable",
                table: "ContentFields",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
