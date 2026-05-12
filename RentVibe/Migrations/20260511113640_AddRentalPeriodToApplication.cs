using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentVibe.Migrations
{
    
    public partial class AddRentalPeriodToApplication : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RentalEndDate",
                table: "RentalApplications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "RentalStartDate",
                table: "RentalApplications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RentalEndDate",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "RentalStartDate",
                table: "RentalApplications");
        }
    }
}
