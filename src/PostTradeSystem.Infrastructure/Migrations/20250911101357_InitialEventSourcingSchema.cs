using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostTradeSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialEventSourcingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventStore",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AggregateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartitionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CausedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartitionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastProcessedVersion = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AggregateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartitionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_AggregateId_Version",
                table: "EventStore",
                columns: new[] { "AggregateId", "AggregateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_CorrelationId",
                table: "EventStore",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_EventId",
                table: "EventStore",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_IsProcessed_CreatedAt",
                table: "EventStore",
                columns: new[] { "IsProcessed", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_PartitionKey",
                table: "EventStore",
                column: "PartitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_AggregateId_RequestHash",
                table: "IdempotencyKeys",
                columns: new[] { "AggregateId", "RequestHash" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_IdempotencyKey",
                table: "IdempotencyKeys",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projections_PartitionKey",
                table: "Projections",
                column: "PartitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_Projections_ProjectionName_AggregateId",
                table: "Projections",
                columns: new[] { "ProjectionName", "AggregateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projections_ProjectionName_LastProcessedVersion",
                table: "Projections",
                columns: new[] { "ProjectionName", "LastProcessedVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AggregateId",
                table: "Snapshots",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AggregateId_Version",
                table: "Snapshots",
                columns: new[] { "AggregateId", "AggregateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_PartitionKey",
                table: "Snapshots",
                column: "PartitionKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventStore");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropTable(
                name: "Projections");

            migrationBuilder.DropTable(
                name: "Snapshots");
        }
    }
}
