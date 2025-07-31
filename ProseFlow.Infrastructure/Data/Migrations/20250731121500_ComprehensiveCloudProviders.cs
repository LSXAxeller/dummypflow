using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComprehensiveCloudProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "CloudApiKey",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "CloudModel",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "CloudTemperature",
                table: "ProviderSettings");

            migrationBuilder.RenameColumn(
                name: "PrimaryProvider",
                table: "ProviderSettings",
                newName: "PrimaryServiceType");

            migrationBuilder.RenameColumn(
                name: "FallbackProvider",
                table: "ProviderSettings",
                newName: "FallbackServiceType");

            migrationBuilder.CreateTable(
                name: "CloudProviderConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Temperature = table.Column<float>(type: "REAL", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudProviderConfigurations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudProviderConfigurations");

            migrationBuilder.RenameColumn(
                name: "PrimaryServiceType",
                table: "ProviderSettings",
                newName: "PrimaryProvider");

            migrationBuilder.RenameColumn(
                name: "FallbackServiceType",
                table: "ProviderSettings",
                newName: "FallbackProvider");

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "ProviderSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CloudApiKey",
                table: "ProviderSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CloudModel",
                table: "ProviderSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "CloudTemperature",
                table: "ProviderSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.UpdateData(
                table: "ProviderSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BaseUrl", "CloudApiKey", "CloudModel", "CloudTemperature" },
                values: new object[] { "https://api.openai.com/v1", "", "gpt-4o", 0.7f });
        }
    }
}
