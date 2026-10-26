using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Data.Migrations;

public partial class UniquePaymentPerAppointment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_payments_appointment_id",
            table: "payments");

        migrationBuilder.CreateIndex(
            name: "ix_payments_appointment_id",
            table: "payments",
            column: "appointment_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_payments_appointment_id",
            table: "payments");

        migrationBuilder.CreateIndex(
            name: "ix_payments_appointment_id",
            table: "payments",
            column: "appointment_id");
    }
}
