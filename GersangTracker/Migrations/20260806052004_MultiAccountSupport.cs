using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GersangTracker.Migrations
{
    /// <inheritdoc />
    public partial class MultiAccountSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Monsters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Monsters_AccountId",
                table: "Monsters",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Monsters_Account_AccountId",
                table: "Monsters",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Monsters_Account_AccountId",
                table: "Monsters");

            migrationBuilder.DropIndex(
                name: "IX_Monsters_AccountId",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Monsters");
        }
    }
}
