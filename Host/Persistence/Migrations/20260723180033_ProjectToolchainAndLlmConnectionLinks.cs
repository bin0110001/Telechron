using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telechron.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectToolchainAndLlmConnectionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LlmConnectionId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToolchainId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LlmConnectionId",
                table: "Projects",
                column: "LlmConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ToolchainId",
                table: "Projects",
                column: "ToolchainId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_LlmConnections_LlmConnectionId",
                table: "Projects",
                column: "LlmConnectionId",
                principalTable: "LlmConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Toolchains_ToolchainId",
                table: "Projects",
                column: "ToolchainId",
                principalTable: "Toolchains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_LlmConnections_LlmConnectionId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Toolchains_ToolchainId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_LlmConnectionId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ToolchainId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LlmConnectionId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ToolchainId",
                table: "Projects");
        }
    }
}
