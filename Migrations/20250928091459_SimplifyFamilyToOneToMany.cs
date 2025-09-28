using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zabota.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyFamilyToOneToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FamilyMembers\" CASCADE;");
            migrationBuilder.Sql(
                @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'Families'
    ) THEN
        CREATE TABLE ""Families"" (
            ""Id"" uuid NOT NULL,
            ""Name"" character varying(200) NOT NULL,
            ""InviteCode"" character varying(12) NOT NULL,
            ""CreatedAtUtc"" timestamp with time zone NOT NULL,
            CONSTRAINT ""PK_Families"" PRIMARY KEY (""Id"")
        );
        CREATE UNIQUE INDEX ""IX_Families_InviteCode"" ON ""Families"" (""InviteCode"");
    END IF;
END $$;
"
            );
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "Users",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsFamilyAdmin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<string>(
                name: "RoleInFamily",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Member"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_FamilyId",
                table: "Users",
                column: "FamilyId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Families_FamilyId",
                table: "Users",
                column: "FamilyId",
                principalTable: "Families",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Users_Families_FamilyId", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Users_FamilyId", table: "Users");

            migrationBuilder.DropColumn(name: "FamilyId", table: "Users");

            migrationBuilder.DropColumn(name: "IsFamilyAdmin", table: "Users");

            migrationBuilder.DropColumn(name: "IsPremium", table: "Users");

            migrationBuilder.DropColumn(name: "RoleInFamily", table: "Users");

            migrationBuilder.CreateTable(
                name: "FamilyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    RoleInFamily = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_FamilyMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_FamilyId_UserId",
                table: "FamilyMembers",
                columns: new[] { "FamilyId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_UserId",
                table: "FamilyMembers",
                column: "UserId"
            );
        }
    }
}
