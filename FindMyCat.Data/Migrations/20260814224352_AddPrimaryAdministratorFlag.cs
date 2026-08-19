using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindMyCat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryAdministratorFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryAdministrator",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "IsPrimaryAdministrator" = 1
                WHERE "Id" = (
                    SELECT "Id" FROM "Users"
                    WHERE "Role" = 'Administrator'
                    ORDER BY "CreatedAt"
                    LIMIT 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrimaryAdministrator",
                table: "Users");
        }
    }
}
