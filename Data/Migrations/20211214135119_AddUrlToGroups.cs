using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docshareqr_link.Data.Migrations
{
    public partial class AddUrlToGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "DocGroups",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Url",
                table: "DocGroups");
        }
    }
}
