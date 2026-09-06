using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeadlessCms.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContentDefinitionNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "ContentTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ContentFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ContentTypes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ContentFields",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"ContentTypes\" SET \"Name\" = \"Key\";");
            migrationBuilder.Sql("UPDATE \"ContentFields\" SET \"Name\" = \"Key\";");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ContentTypes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ContentFields",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "");
        }
    }
}
