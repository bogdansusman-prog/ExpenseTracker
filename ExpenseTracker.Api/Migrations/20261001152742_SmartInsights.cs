using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ExpenseTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class SmartInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegretRatedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegretScore",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "HourlyRate", "Language" },
                values: new object[] { 1, null, "ro" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_RegretScore",
                table: "Transactions",
                sql: "\"RegretScore\" IS NULL OR (\"RegretScore\" BETWEEN 1 AND 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_RegretScore",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RegretRatedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RegretScore",
                table: "Transactions");
        }
    }
}
