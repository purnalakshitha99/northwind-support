using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaWindowStartAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SlaWindowStartAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaWindowStartAtUtc",
                table: "Tickets");
        }
    }
}
