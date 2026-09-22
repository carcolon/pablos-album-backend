using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabyAlbum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptionalPhotoAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_AlbumPages_AlbumPageId",
                table: "Photos");

            migrationBuilder.AlterColumn<Guid>(
                name: "AlbumPageId",
                table: "Photos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AlbumId",
                table: "Photos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE "Photos" AS photo
                SET "AlbumId" = page."AlbumId"
                FROM "AlbumPages" AS page
                WHERE photo."AlbumPageId" = page."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Photos_AlbumId",
                table: "Photos",
                column: "AlbumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_AlbumPages_AlbumPageId",
                table: "Photos",
                column: "AlbumPageId",
                principalTable: "AlbumPages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_Albums_AlbumId",
                table: "Photos",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_AlbumPages_AlbumPageId",
                table: "Photos");

            migrationBuilder.DropForeignKey(
                name: "FK_Photos_Albums_AlbumId",
                table: "Photos");

            migrationBuilder.DropIndex(
                name: "IX_Photos_AlbumId",
                table: "Photos");

            migrationBuilder.Sql("""
                UPDATE "Photos" AS photo
                SET "AlbumPageId" = page."Id"
                FROM (
                    SELECT DISTINCT ON ("AlbumId") "AlbumId", "Id"
                    FROM "AlbumPages"
                    ORDER BY "AlbumId", "PageNumber"
                ) AS page
                WHERE photo."AlbumPageId" IS NULL
                    AND photo."AlbumId" = page."AlbumId";
                """);

            migrationBuilder.DropColumn(
                name: "AlbumId",
                table: "Photos");

            migrationBuilder.AlterColumn<Guid>(
                name: "AlbumPageId",
                table: "Photos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_AlbumPages_AlbumPageId",
                table: "Photos",
                column: "AlbumPageId",
                principalTable: "AlbumPages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
