using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongDoan.Infrastructure.Cms.Migrations
{
    /// <inheritdoc />
    public partial class InitialCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cms");

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaUploads",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DeclaredSize = table.Column<long>(type: "bigint", nullable: false),
                    ChunkSize = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaUsages",
                schema: "cms",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaUsages", x => new { x.MediaId, x.OwnerType, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_MediaUsages_MediaAssets_MediaId",
                        column: x => x.MediaId,
                        principalSchema: "cms",
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_IsDeleted_CreatedUtc",
                schema: "cms",
                table: "MediaAssets",
                columns: new[] { "IsDeleted", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_StoredPath",
                schema: "cms",
                table: "MediaAssets",
                column: "StoredPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaUsages_OwnerType_OwnerId",
                schema: "cms",
                table: "MediaUsages",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaUploads",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "MediaUsages",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "MediaAssets",
                schema: "cms");
        }
    }
}
