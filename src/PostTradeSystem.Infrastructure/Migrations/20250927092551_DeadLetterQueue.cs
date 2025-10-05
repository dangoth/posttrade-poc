using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostTradeSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeadLetterQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeadLetterReason",
                table: "OutboxEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "OutboxEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeadLettered",
                table: "OutboxEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_IsDeadLettered_DeadLetteredAt",
                table: "OutboxEvents",
                columns: new[] { "IsDeadLettered", "DeadLetteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_ProcessingStatus",
                table: "OutboxEvents",
                columns: new[] { "IsProcessed", "IsDeadLettered", "RetryCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_IsDeadLettered_DeadLetteredAt",
                table: "OutboxEvents");

            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_ProcessingStatus",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "DeadLetterReason",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "IsDeadLettered",
                table: "OutboxEvents");
        }
    }
}
