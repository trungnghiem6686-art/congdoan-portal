using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongDoan.Infrastructure.Exams.Migrations
{
    /// <inheritdoc />
    public partial class AttemptWriter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousWriterToken",
                schema: "exam",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WriterSeenUtc",
                schema: "exam",
                table: "Attempts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousWriterToken",
                schema: "exam",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "WriterSeenUtc",
                schema: "exam",
                table: "Attempts");
        }
    }
}
