using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityStampAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectMedia_ProjectId",
                table: "ProjectMedia");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "AdminUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Existing rows may carry blank or duplicate slugs (they used to be backfilled by the
            // seeder, which runs after migrations) — repair them so the unique index can be created.
            migrationBuilder.Sql(
                """
                UPDATE "Projects" p
                SET "Slug" = CASE WHEN p."Slug" = '' THEN 'project-' || p."Id" ELSE p."Slug" || '-' || p."Id" END
                WHERE p."Slug" = ''
                   OR EXISTS (SELECT 1 FROM "Projects" q WHERE q."Slug" = p."Slug" AND q."Id" < p."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Slug",
                table: "Projects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMedia_ContentHash",
                table: "ProjectMedia",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMedia_ProjectId_SortOrder",
                table: "ProjectMedia",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Slug",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMedia_ContentHash",
                table: "ProjectMedia");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMedia_ProjectId_SortOrder",
                table: "ProjectMedia");

            migrationBuilder.DropIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "AdminUsers");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMedia_ProjectId",
                table: "ProjectMedia",
                column: "ProjectId");
        }
    }
}
