using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalModelIdleTimeout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LocalModelAutoUnloadEnabled",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LocalModelIdleTimeoutMinutes",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "ProviderSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LocalModelAutoUnloadEnabled", "LocalModelIdleTimeoutMinutes" },
                values: new object[] { true, 30 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalModelAutoUnloadEnabled",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "LocalModelIdleTimeoutMinutes",
                table: "ProviderSettings");
        }
    }
}
