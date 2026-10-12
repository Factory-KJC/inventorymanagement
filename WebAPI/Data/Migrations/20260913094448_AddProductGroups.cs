using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductGroupId",
                schema: "inventorymanagement",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_groups",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_groups_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "inventorymanagement",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_ProductGroupId",
                schema: "inventorymanagement",
                table: "products",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_product_groups_HouseholdId_Name",
                schema: "inventorymanagement",
                table: "product_groups",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_groups_ProductGroupId",
                schema: "inventorymanagement",
                table: "products",
                column: "ProductGroupId",
                principalSchema: "inventorymanagement",
                principalTable: "product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_product_groups_ProductGroupId",
                schema: "inventorymanagement",
                table: "products");

            migrationBuilder.DropTable(
                name: "product_groups",
                schema: "inventorymanagement");

            migrationBuilder.DropIndex(
                name: "IX_products_ProductGroupId",
                schema: "inventorymanagement",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductGroupId",
                schema: "inventorymanagement",
                table: "products");
        }
    }
}
