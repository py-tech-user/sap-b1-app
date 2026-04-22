using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapB1App.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndSalesMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "SapSalesPersonCode",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Ensure existing rows are unique before creating the unique index.
            migrationBuilder.Sql("""
                UPDATE [Users]
                SET [SapSalesPersonCode] = [Id]
                WHERE [SapSalesPersonCode] = 0;
            """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SapSalesPersonCode",
                table: "Users",
                column: "SapSalesPersonCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_SapSalesPersonCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SapSalesPersonCode",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);
        }
    }
}
