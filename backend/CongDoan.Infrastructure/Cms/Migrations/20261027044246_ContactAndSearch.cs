using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongDoan.Infrastructure.Cms.Migrations
{
    /// <inheritdoc />
    public partial class ContactAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                schema: "cms",
                table: "Albums",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ContactRequests",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    HandlerNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HandledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandledUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactRequests_Status_CreatedUtc",
                schema: "cms",
                table: "ContactRequests",
                columns: new[] { "Status", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactRequests",
                schema: "cms");

            migrationBuilder.DropColumn(
                name: "SearchText",
                schema: "cms",
                table: "Albums");
        }
    }
}
