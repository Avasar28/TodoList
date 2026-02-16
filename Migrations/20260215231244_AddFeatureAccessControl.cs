using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoListApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    TechnicalName = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    FeatureId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFeatures_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFeatures_SystemFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "SystemFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFeatures_FeatureId",
                table: "UserFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeatures_UserId",
                table: "UserFeatures",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFeatures");

            migrationBuilder.DropTable(
                name: "SystemFeatures");
        }
    }
}
