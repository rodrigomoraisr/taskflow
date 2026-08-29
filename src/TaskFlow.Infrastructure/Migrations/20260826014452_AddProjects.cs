using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.UniqueConstraint("AK_Projects_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_Projects_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "Workspaces"
                (
                    "Id",
                    "Name",
                    "IsDeleted",
                    "DeletedAt",
                    "CreatedAt",
                    "UpdatedAt"
                )
                SELECT DISTINCT
                    task."WorkspaceId",
                    'Archived Legacy Workspace',
                    TRUE,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM tasks AS task
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM "Workspaces" AS workspace
                    WHERE workspace."Id" = task."WorkspaceId"
                );

                WITH imported_projects AS
                (
                    INSERT INTO "Projects"
                    (
                        "Id",
                        "WorkspaceId",
                        "Name",
                        "Description",
                        "Status",
                        "IsDeleted",
                        "DeletedAt",
                        "CreatedAt",
                        "UpdatedAt"
                    )
                    SELECT
                        gen_random_uuid(),
                        task."WorkspaceId",
                        'Imported Tasks',
                        'Automatically created for tasks that existed before Projects were introduced.',
                        'Active',
                        workspace."IsDeleted",
                        CASE
                            WHEN workspace."IsDeleted" THEN CURRENT_TIMESTAMP
                            ELSE NULL
                        END,
                        CURRENT_TIMESTAMP,
                        NULL
                    FROM tasks AS task
                    INNER JOIN "Workspaces" AS workspace
                        ON workspace."Id" = task."WorkspaceId"
                    GROUP BY task."WorkspaceId", workspace."IsDeleted"
                    RETURNING "Id", "WorkspaceId"
                )
                UPDATE tasks AS task
                SET "ProjectId" = imported_project."Id"
                FROM imported_projects AS imported_project
                WHERE task."WorkspaceId" = imported_project."WorkspaceId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_WorkspaceId_ProjectId_IsDeleted",
                table: "tasks",
                columns: new[] { "WorkspaceId", "ProjectId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkspaceId_IsDeleted",
                table: "Projects",
                columns: new[] { "WorkspaceId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_Projects_WorkspaceId_ProjectId",
                table: "tasks",
                columns: new[] { "WorkspaceId", "ProjectId" },
                principalTable: "Projects",
                principalColumns: new[] { "WorkspaceId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_Projects_WorkspaceId_ProjectId",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.Sql(
                """
                DELETE FROM "Workspaces" AS workspace
                WHERE workspace."Name" = 'Archived Legacy Workspace'
                  AND workspace."IsDeleted" = TRUE
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM "WorkspaceUsers" AS membership
                      WHERE membership."WorkspaceId" = workspace."Id"
                  );
                """);

            migrationBuilder.DropIndex(
                name: "IX_tasks_WorkspaceId_ProjectId_IsDeleted",
                table: "tasks");
        }
    }
}
