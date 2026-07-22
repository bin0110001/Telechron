using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telechron.Host.Security.Audit.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: false),
                    PriorHash = table.Column<string>(type: "TEXT", nullable: false),
                    RecordHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Sequence);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAtUtc",
                table: "AuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ProjectId",
                table: "AuditEvents",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
