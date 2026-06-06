using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TorrentManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovieDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TorrentFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    QBitTorrentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieDownloads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovieDownloads_QBitTorrentHash",
                table: "MovieDownloads",
                column: "QBitTorrentHash");

            migrationBuilder.CreateIndex(
                name: "IX_MovieDownloads_Status",
                table: "MovieDownloads",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieDownloads");
        }
    }
}
