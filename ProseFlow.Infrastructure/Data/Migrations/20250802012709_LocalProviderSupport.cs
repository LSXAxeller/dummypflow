using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalProviderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocalModelContextSize",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocalModelMaxTokens",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LocalModelMemoryMap",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LocalModelMemorylock",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "LocalModelTemperature",
                table: "ProviderSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.UpdateData(
                table: "ProviderSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LocalModelContextSize", "LocalModelMaxTokens", "LocalModelMemoryMap", "LocalModelMemorylock", "LocalModelTemperature" },
                values: new object[] { 4096, 2048, true, false, 0.7f });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalModelContextSize",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "LocalModelMaxTokens",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "LocalModelMemoryMap",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "LocalModelMemorylock",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "LocalModelTemperature",
                table: "ProviderSettings");
        }
    }
}
