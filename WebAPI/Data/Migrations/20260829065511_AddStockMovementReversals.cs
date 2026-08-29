using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReversesMovementId",
                schema: "inventorymanagement",
                table: "stock_movements",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ReversesMovementId",
                schema: "inventorymanagement",
                table: "stock_movements",
                column: "ReversesMovementId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_stock_movements_ReversesMovementId",
                schema: "inventorymanagement",
                table: "stock_movements",
                column: "ReversesMovementId",
                principalSchema: "inventorymanagement",
                principalTable: "stock_movements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_stock_movements_ReversesMovementId",
                schema: "inventorymanagement",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_ReversesMovementId",
                schema: "inventorymanagement",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ReversesMovementId",
                schema: "inventorymanagement",
                table: "stock_movements");
        }
    }
}
