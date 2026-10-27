using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongDoan.Infrastructure.Exams.Migrations
{
    /// <inheritdoc />
    public partial class Exams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttemptEvents",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exams",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OpensAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosesAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    ScoreRule = table.Column<int>(type: "int", nullable: false),
                    PassScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RoundingDecimals = table.Column<int>(type: "int", nullable: false),
                    ResultRelease = table.Column<int>(type: "int", nullable: false),
                    ShowAnswerDetail = table.Column<bool>(type: "bit", nullable: false),
                    ShowExplanation = table.Column<bool>(type: "bit", nullable: false),
                    ShuffleQuestions = table.Column<bool>(type: "bit", nullable: false),
                    ShuffleChoices = table.Column<bool>(type: "bit", nullable: false),
                    LogTabSwitches = table.Column<bool>(type: "bit", nullable: false),
                    QuestionSource = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResultsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ResultsApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamTemplates",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attempts",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Points = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    MaxScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    Passed = table.Column<bool>(type: "bit", nullable: true),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    LastSeq = table.Column<long>(type: "bigint", nullable: false),
                    WriterToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TabSwitchCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attempts_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "exam",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamAssignments",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamAssignments_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "exam",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamMatrixRules",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Difficulty = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true),
                    Count = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamMatrixRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamMatrixRules_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "exam",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestions",
                schema: "exam",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestions", x => new { x.ExamId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_ExamQuestions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "exam",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "exam",
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttemptQuestions",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNo = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Points = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    ChoicesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "exam",
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GradeRevisions",
                schema: "exam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldScore = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    NewScore = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    By = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeRevisions_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "exam",
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Answers",
                schema: "exam",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Flagged = table.Column<bool>(type: "bit", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    SavedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AwardedPoints = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answers", x => new { x.AttemptId, x.AttemptQuestionId });
                    table.ForeignKey(
                        name: "FK_Answers_AttemptQuestions_AttemptQuestionId",
                        column: x => x.AttemptQuestionId,
                        principalSchema: "exam",
                        principalTable: "AttemptQuestions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Answers_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "exam",
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Answers_AttemptQuestionId",
                schema: "exam",
                table: "Answers",
                column: "AttemptQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptEvents_AttemptId",
                schema: "exam",
                table: "AttemptEvents",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_AttemptId_OrderNo",
                schema: "exam",
                table: "AttemptQuestions",
                columns: new[] { "AttemptId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_Status_DeadlineUtc",
                schema: "exam",
                table: "Attempts",
                columns: new[] { "Status", "DeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Attempt_No",
                schema: "exam",
                table: "Attempts",
                columns: new[] { "ExamId", "CandidateId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Attempt_OneInProgress",
                schema: "exam",
                table: "Attempts",
                columns: new[] { "ExamId", "CandidateId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_ExamId_TargetType_TargetId",
                schema: "exam",
                table: "ExamAssignments",
                columns: new[] { "ExamId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_TargetType_TargetId",
                schema: "exam",
                table: "ExamAssignments",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamMatrixRules_ExamId",
                schema: "exam",
                table: "ExamMatrixRules",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_QuestionId",
                schema: "exam",
                table: "ExamQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Status_IsDeleted_OpensAtUtc",
                schema: "exam",
                table: "Exams",
                columns: new[] { "Status", "IsDeleted", "OpensAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeRevisions_AttemptId",
                schema: "exam",
                table: "GradeRevisions",
                column: "AttemptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Answers",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "AttemptEvents",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "ExamAssignments",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "ExamMatrixRules",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "ExamQuestions",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "ExamTemplates",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "GradeRevisions",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "AttemptQuestions",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "Attempts",
                schema: "exam");

            migrationBuilder.DropTable(
                name: "Exams",
                schema: "exam");
        }
    }
}
