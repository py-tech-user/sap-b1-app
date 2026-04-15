using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapB1App.Migrations
{
    /// <inheritdoc />
    public partial class AddSapLikeOptionalDocumentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryNotes_Orders_OrderId",
                table: "DeliveryNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_DeliveryNotes_DeliveryNoteId",
                table: "Invoices");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "Quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "Quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseType",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardCode",
                table: "Quotes",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "QuoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "QuoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "QuoteLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "QuoteLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseType",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardCode",
                table: "Orders",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "OrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "OrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "OrderLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "OrderLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "DeliveryNoteId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseType",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardCode",
                table: "Invoices",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "InvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "InvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "InvoiceLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "InvoiceLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "DeliveryNotes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "DeliveryNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "DeliveryNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseType",
                table: "DeliveryNotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardCode",
                table: "DeliveryNotes",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BaseEntry",
                table: "DeliveryNoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseLine",
                table: "DeliveryNoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "DeliveryNoteLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "DeliveryNoteLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryNotes_Orders_OrderId",
                table: "DeliveryNotes",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_DeliveryNotes_DeliveryNoteId",
                table: "Invoices",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryNotes_Orders_OrderId",
                table: "DeliveryNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_DeliveryNotes_DeliveryNoteId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "BaseType",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CardCode",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BaseType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CardCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CardCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "BaseType",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "CardCode",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "BaseEntry",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "BaseLine",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "DeliveryNoteLines");

            migrationBuilder.AlterColumn<int>(
                name: "DeliveryNoteId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "DeliveryNotes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryNotes_Orders_OrderId",
                table: "DeliveryNotes",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_DeliveryNotes_DeliveryNoteId",
                table: "Invoices",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
