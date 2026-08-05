using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InventoryAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventoryDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventorymanagement");

            migrationBuilder.CreateTable(
                name: "households",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Password_Hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_locations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "inventorymanagement",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "inventorymanagement",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_operations",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_operations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "inventorymanagement",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_lots",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_lots_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "inventorymanagement",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_lots_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventorymanagement",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_lots_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventorymanagement",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventorymanagement",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_movements_stock_lots_StockLotId",
                        column: x => x.StockLotId,
                        principalSchema: "inventorymanagement",
                        principalTable: "stock_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_stock_operations_StockOperationId",
                        column: x => x.StockOperationId,
                        principalSchema: "inventorymanagement",
                        principalTable: "stock_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "inventorymanagement",
                table: "households",
                columns: new[] { "Id", "Name", "TimeZone" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "自宅", "Asia/Tokyo" });

            migrationBuilder.InsertData(
                schema: "inventorymanagement",
                table: "locations",
                columns: new[] { "Id", "HouseholdId", "Name", "SortOrder" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001"), "未設定", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_locations_HouseholdId_Name",
                schema: "inventorymanagement",
                table: "locations",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_HouseholdId_Barcode",
                schema: "inventorymanagement",
                table: "products",
                columns: new[] { "HouseholdId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_HouseholdId_ProductId_LocationId_ExpiresOn",
                schema: "inventorymanagement",
                table: "stock_lots",
                columns: new[] { "HouseholdId", "ProductId", "LocationId", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_LocationId",
                schema: "inventorymanagement",
                table: "stock_lots",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_ProductId",
                schema: "inventorymanagement",
                table: "stock_lots",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId_OccurredAt",
                schema: "inventorymanagement",
                table: "stock_movements",
                columns: new[] { "ProductId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_StockLotId",
                schema: "inventorymanagement",
                table: "stock_movements",
                column: "StockLotId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_StockOperationId",
                schema: "inventorymanagement",
                table: "stock_movements",
                column: "StockOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_operations_HouseholdId_IdempotencyKey",
                schema: "inventorymanagement",
                table: "stock_operations",
                columns: new[] { "HouseholdId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                schema: "inventorymanagement",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventorymanagement");

            migrationBuilder.DropTable(
                name: "users",
                schema: "inventorymanagement");

            migrationBuilder.DropTable(
                name: "stock_lots",
                schema: "inventorymanagement");

            migrationBuilder.DropTable(
                name: "stock_operations",
                schema: "inventorymanagement");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "inventorymanagement");

            migrationBuilder.DropTable(
                name: "products",
                schema: "inventorymanagement");

            migrationBuilder.DropTable(
                name: "households",
                schema: "inventorymanagement");
        }
    }
}
