using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations;

public partial class AddVoiceFieldsToDialogMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "VoiceEnabled",
            table: "DialogModes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "VoiceId",
            table: "DialogModes",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "VoiceEnabled",
            table: "DialogModes");

        migrationBuilder.DropColumn(
            name: "VoiceId",
            table: "DialogModes");
    }
}
