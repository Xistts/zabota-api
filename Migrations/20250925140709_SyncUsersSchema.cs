using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zabota.Migrations
{
    /// <inheritdoc />
    public partial class SyncUsersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Users_Login"";");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);
        }
    }
}
