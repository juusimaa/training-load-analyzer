using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingLoadAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    AthleteId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResumePointUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSyncStartedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    LastOutcome = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.AthleteId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncStates");
        }
    }
}
