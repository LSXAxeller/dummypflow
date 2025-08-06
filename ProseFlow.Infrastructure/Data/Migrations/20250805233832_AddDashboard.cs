using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompletionTokens",
                table: "History",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<double>(
                name: "LatencyMs",
                table: "History",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<long>(
                name: "PromptTokens",
                table: "History",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "History");

            migrationBuilder.DropColumn(
                name: "LatencyMs",
                table: "History");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "History");
        }
    }
}
