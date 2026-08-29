using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E3A.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class scan003 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScanReportJson",
                table: "ItemVersions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScanReportJson",
                table: "ItemVersions");
        }
    }
}
