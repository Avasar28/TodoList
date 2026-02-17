using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoListApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPasskeyEnabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasskeyHash",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPasskeyEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PasskeyHash",
                table: "AspNetUsers");
        }
    }
}
