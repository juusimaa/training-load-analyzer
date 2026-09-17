using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingLoadAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Activities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAtOffsetMinutes = table.Column<short>(type: "INTEGER", nullable: false),
                    MovingTimeTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    HeartRateJson = table.Column<string>(type: "TEXT", nullable: true),
                    HeartRateOutstanding = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => new { x.Provider, x.ExternalId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_StartedAtUtcTicks",
                table: "Activities",
                column: "StartedAtUtcTicks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
