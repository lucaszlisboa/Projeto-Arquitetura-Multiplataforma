using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dentist_service.Migrations
{
    /// <inheritdoc />
    public partial class dentistServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dentist_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dentist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_periodic = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dentist_services", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dentist_services_dentist_id",
                table: "dentist_services",
                column: "dentist_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dentist_services");
        }
    }
}
