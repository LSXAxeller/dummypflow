using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageStatisticsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LocalModelLoadOnStartup",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UsageStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    PromptTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletionTokens = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageStatistics", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ProviderSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "LocalModelLoadOnStartup",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_UsageStatistics_Year_Month",
                table: "UsageStatistics",
                columns: new[] { "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageStatistics");

            migrationBuilder.DropColumn(
                name: "LocalModelLoadOnStartup",
                table: "ProviderSettings");
        }
    }
}
