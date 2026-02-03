using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.API.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddOutboxBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxBatches_CreatedAt",
                table: "OutboxBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxBatches_NextAttemptAt",
                table: "OutboxBatches",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxBatches_Status",
                table: "OutboxBatches",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxBatches");
        }
    }
}
