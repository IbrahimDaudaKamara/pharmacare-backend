using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediCorePMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetCodeExpires",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Refunded",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPrescription",
                table: "Medicines",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetCodeExpires",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Refunded",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RequiresPrescription",
                table: "Medicines");
        }
    }
}
