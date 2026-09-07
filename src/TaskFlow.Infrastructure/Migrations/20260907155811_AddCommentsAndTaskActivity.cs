using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentsAndTaskActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_tasks_WorkspaceId_Id",
                table: "tasks",
                columns: new[] { "WorkspaceId", "Id" });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                    table.UniqueConstraint("AK_comments_WorkspaceId_TaskId_Id", x => new { x.WorkspaceId, x.TaskId, x.Id });
                    table.ForeignKey(
                        name: "FK_comments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_comments_tasks_WorkspaceId_TaskId",
                        columns: x => new { x.WorkspaceId, x.TaskId },
                        principalTable: "tasks",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_activities_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_activities_comments_WorkspaceId_TaskId_CommentId",
                        columns: x => new { x.WorkspaceId, x.TaskId, x.CommentId },
                        principalTable: "comments",
                        principalColumns: new[] { "WorkspaceId", "TaskId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_activities_tasks_WorkspaceId_TaskId",
                        columns: x => new { x.WorkspaceId, x.TaskId },
                        principalTable: "tasks",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comments_AuthorId",
                table: "comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_WorkspaceId_TaskId_IsDeleted_CreatedAt_Id",
                table: "comments",
                columns: new[] { "WorkspaceId", "TaskId", "IsDeleted", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_activities_ActorId",
                table: "task_activities",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_task_activities_WorkspaceId_CreatedAt_Id",
                table: "task_activities",
                columns: new[] { "WorkspaceId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_activities_WorkspaceId_TaskId_CommentId",
                table: "task_activities",
                columns: new[] { "WorkspaceId", "TaskId", "CommentId" });

            migrationBuilder.CreateIndex(
                name: "IX_task_activities_WorkspaceId_TaskId_CreatedAt_Id",
                table: "task_activities",
                columns: new[] { "WorkspaceId", "TaskId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_activities");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_tasks_WorkspaceId_Id",
                table: "tasks");
        }
    }
}
