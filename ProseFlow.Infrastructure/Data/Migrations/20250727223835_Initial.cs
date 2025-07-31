using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", nullable: false),
                    Instruction = table.Column<string>(type: "TEXT", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: false),
                    OpenInWindow = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExplainChanges = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApplicationContext = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneralSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActionMenuHotkey = table.Column<string>(type: "TEXT", nullable: false),
                    SmartPasteHotkey = table.Column<string>(type: "TEXT", nullable: false),
                    SmartPasteActionId = table.Column<int>(type: "INTEGER", nullable: true),
                    LaunchAtLogin = table.Column<bool>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActionName = table.Column<string>(type: "TEXT", nullable: false),
                    InputText = table.Column<string>(type: "TEXT", nullable: false),
                    OutputText = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderUsed = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CloudApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    CloudModel = table.Column<string>(type: "TEXT", nullable: false),
                    CloudTemperature = table.Column<float>(type: "REAL", nullable: false),
                    LocalModelPath = table.Column<string>(type: "TEXT", nullable: false),
                    LocalCpuCores = table.Column<int>(type: "INTEGER", nullable: false),
                    PreferGpu = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrimaryProvider = table.Column<string>(type: "TEXT", nullable: false),
                    FallbackProvider = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GeneralSettings",
                columns: new[] { "Id", "ActionMenuHotkey", "LaunchAtLogin", "SmartPasteActionId", "SmartPasteHotkey", "Theme" },
                values: new object[] { 1, "Ctrl+J", false, null, "Ctrl+Shift+V", "System" });

            migrationBuilder.InsertData(
                table: "ProviderSettings",
                columns: new[] { "Id", "BaseUrl", "CloudApiKey", "CloudModel", "CloudTemperature", "FallbackProvider", "LocalCpuCores", "LocalModelPath", "PreferGpu", "PrimaryProvider" },
                values: new object[] { 1, "https://api.openai.com/v1", "", "gpt-4o", 0.7f, "None", 4, "", true, "Cloud" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "GeneralSettings");

            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "ProviderSettings");
        }
    }
}
