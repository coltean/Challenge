using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.API.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class FixIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookEvents_ProcessedAt",
                table: "WebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Entities_DeletedAt",
                table: "Entities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ProcessedAt",
                table: "WebhookEvents",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_DeletedAt",
                table: "Entities",
                column: "DeletedAt");
        }
    }
}
