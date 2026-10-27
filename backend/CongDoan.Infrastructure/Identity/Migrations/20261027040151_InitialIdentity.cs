using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongDoan.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Candidates",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_OrgUnitId",
                schema: "identity",
                table: "AdminUsers",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_UserName",
                schema: "identity",
                table: "AdminUsers",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_MemberId",
                schema: "identity",
                table: "Candidates",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_OrgUnitId",
                schema: "identity",
                table: "Candidates",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_UserName",
                schema: "identity",
                table: "Candidates",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Kind_AccountId",
                schema: "identity",
                table: "Sessions",
                columns: new[] { "Kind", "AccountId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Candidates",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "identity");
        }
    }
}
