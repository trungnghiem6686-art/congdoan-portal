using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongDoan.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class Rbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgUnits",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgUnits_OrgUnits_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "identity",
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateGroups",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateGroups_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalSchema: "identity",
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminUserRoles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "identity",
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalSchema: "identity",
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateGroupMembers",
                schema: "identity",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateGroupMembers", x => new { x.GroupId, x.CandidateId });
                    table.ForeignKey(
                        name: "FK_CandidateGroupMembers_CandidateGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "identity",
                        principalTable: "CandidateGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateGroupMembers_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "identity",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserRoles_AdminUserId_RoleId_OrgUnitId",
                schema: "identity",
                table: "AdminUserRoles",
                columns: new[] { "AdminUserId", "RoleId", "OrgUnitId" },
                unique: true,
                filter: "[OrgUnitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserRoles_OrgUnitId",
                schema: "identity",
                table: "AdminUserRoles",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserRoles_RoleId",
                schema: "identity",
                table: "AdminUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateGroupMembers_CandidateId",
                schema: "identity",
                table: "CandidateGroupMembers",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateGroups_OrgUnitId",
                schema: "identity",
                table: "CandidateGroups",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_Code",
                schema: "identity",
                table: "OrgUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_ParentId",
                schema: "identity",
                table: "OrgUnits",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                schema: "identity",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_OrgUnits_OrgUnitId",
                schema: "identity",
                table: "AdminUsers",
                column: "OrgUnitId",
                principalSchema: "identity",
                principalTable: "OrgUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Candidates_OrgUnits_OrgUnitId",
                schema: "identity",
                table: "Candidates",
                column: "OrgUnitId",
                principalSchema: "identity",
                principalTable: "OrgUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_OrgUnits_OrgUnitId",
                schema: "identity",
                table: "AdminUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Candidates_OrgUnits_OrgUnitId",
                schema: "identity",
                table: "Candidates");

            migrationBuilder.DropTable(
                name: "AdminUserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "CandidateGroupMembers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "CandidateGroups",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "OrgUnits",
                schema: "identity");
        }
    }
}
