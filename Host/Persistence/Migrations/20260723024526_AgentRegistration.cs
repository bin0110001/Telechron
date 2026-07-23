using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telechron.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MachineFingerprint",
                table: "Machines",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AgentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSessions_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Machines_MachineFingerprint",
                table: "Machines",
                column: "MachineFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_MachineId",
                table: "AgentSessions",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_SessionTokenHash",
                table: "AgentSessions",
                column: "SessionTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSessions");

            migrationBuilder.DropIndex(
                name: "IX_Machines_MachineFingerprint",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "MachineFingerprint",
                table: "Machines");
        }
    }
}
