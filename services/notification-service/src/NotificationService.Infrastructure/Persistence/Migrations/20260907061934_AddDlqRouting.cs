using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDlqRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dlq_message_id",
                schema: "notifications",
                table: "notification_records",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dlq_routed_at",
                schema: "notifications",
                table: "notification_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_message_json",
                schema: "notifications",
                table: "notification_records",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dlq_message_id",
                schema: "notifications",
                table: "notification_records");

            migrationBuilder.DropColumn(
                name: "dlq_routed_at",
                schema: "notifications",
                table: "notification_records");

            migrationBuilder.DropColumn(
                name: "original_message_json",
                schema: "notifications",
                table: "notification_records");
        }
    }
}
