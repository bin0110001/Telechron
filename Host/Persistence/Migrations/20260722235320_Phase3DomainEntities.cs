using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telechron.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3DomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Connectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ModuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    SecretHandle = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connectors_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DesignDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignDocuments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Functions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ModuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    InputArtifactTypesJson = table.Column<string>(type: "TEXT", nullable: false),
                    OutputArtifactTypesJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeprecated = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModuleVersionMajor = table.Column<int>(type: "INTEGER", nullable: false),
                    ModuleVersionMinor = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Functions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NaturalLanguageRequest = table.Column<string>(type: "TEXT", nullable: false),
                    PlanningPath = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposedWorkflowIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CapabilityGapAnalysisJson = table.Column<string>(type: "TEXT", nullable: true),
                    RequiredModulesJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LlmConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    SecretHandle = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    VersionMajor = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionMinor = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionPatch = table.Column<int>(type: "INTEGER", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    TestCommand = table.Column<string>(type: "TEXT", nullable: false),
                    SourceCodeRef = table.Column<string>(type: "TEXT", nullable: false),
                    InstalledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Toolchains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ModuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuildCommand = table.Column<string>(type: "TEXT", nullable: false),
                    TestCommand = table.Column<string>(type: "TEXT", nullable: false),
                    VerifyCommand = table.Column<string>(type: "TEXT", nullable: false),
                    ExportCommand = table.Column<string>(type: "TEXT", nullable: true),
                    DeployCommand = table.Column<string>(type: "TEXT", nullable: true),
                    EnvironmentRequirementsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toolchains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    FailurePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DesignDocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequirementId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requirements_DesignDocuments_DesignDocumentId",
                        column: x => x.DesignDocumentId,
                        principalTable: "DesignDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Personas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    PromptTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    LlmConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionMode = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedToolsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedConnectorIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedWorkflowIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MaxIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxLlmCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ApprovalPolicyJson = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedSecretHandlesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personas_LlmConnections_LlmConnectionId",
                        column: x => x.LlmConnectionId,
                        principalTable: "LlmConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personas_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ExclusiveGroup = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SuiteResultsJson = table.Column<string>(type: "TEXT", nullable: true),
                    LogsRef = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Runs_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Runs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DefinitionSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequirementRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequirementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementRevisions_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequirementRevisions_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OriginFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    RootCauseSignature = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    FailureClass = table.Column<int>(type: "INTEGER", nullable: false),
                    Fixability = table.Column<string>(type: "TEXT", nullable: true),
                    FixStatus = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Findings_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ArtifactType = table.Column<string>(type: "TEXT", nullable: false),
                    BlobRef = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_WorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RepairAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotRef = table.Column<string>(type: "TEXT", nullable: false),
                    PatchDiff = table.Column<string>(type: "TEXT", nullable: false),
                    VerifyResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovalDecision = table.Column<int>(type: "INTEGER", nullable: true),
                    ApproverUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultingArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CommitReference = table.Column<string>(type: "TEXT", nullable: true),
                    ProvenanceRecordJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairAttempts_Artifacts_ResultingArtifactId",
                        column: x => x.ResultingArtifactId,
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RepairAttempts_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RepairAttemptFindings",
                columns: table => new
                {
                    RepairAttemptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FindingId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairAttemptFindings", x => new { x.RepairAttemptId, x.FindingId });
                    table.ForeignKey(
                        name: "FK_RepairAttemptFindings_Findings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepairAttemptFindings_RepairAttempts_RepairAttemptId",
                        column: x => x.RepairAttemptId,
                        principalTable: "RepairAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_WorkflowRunId",
                table: "Artifacts",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_ProjectId",
                table: "Connectors",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignDocuments_ProjectId",
                table: "DesignDocuments",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ProjectId",
                table: "Findings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_RunId",
                table: "Findings",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentPlans_ProjectId",
                table: "IntentPlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_Name",
                table: "Modules",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_LlmConnectionId",
                table: "Personas",
                column: "LlmConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ProjectId",
                table: "Personas",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairAttemptFindings_FindingId",
                table: "RepairAttemptFindings",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairAttempts_ApproverUserId",
                table: "RepairAttempts",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairAttempts_ResultingArtifactId",
                table: "RepairAttempts",
                column: "ResultingArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementRevisions_ChangedByUserId",
                table: "RequirementRevisions",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementRevisions_RequirementId_RevisionNumber",
                table: "RequirementRevisions",
                columns: new[] { "RequirementId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_DesignDocumentId_RequirementId",
                table: "Requirements",
                columns: new[] { "DesignDocumentId", "RequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ExclusiveGroup",
                table: "Resources",
                column: "ExclusiveGroup");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_MachineId",
                table: "Resources",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_MachineId",
                table: "Runs",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_ProjectId",
                table: "Runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                table: "WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_ProjectId",
                table: "Workflows",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Connectors");

            migrationBuilder.DropTable(
                name: "Functions");

            migrationBuilder.DropTable(
                name: "IntentPlans");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Personas");

            migrationBuilder.DropTable(
                name: "RepairAttemptFindings");

            migrationBuilder.DropTable(
                name: "RequirementRevisions");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Toolchains");

            migrationBuilder.DropTable(
                name: "LlmConnections");

            migrationBuilder.DropTable(
                name: "Findings");

            migrationBuilder.DropTable(
                name: "RepairAttempts");

            migrationBuilder.DropTable(
                name: "Requirements");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "Artifacts");

            migrationBuilder.DropTable(
                name: "DesignDocuments");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "WorkflowRuns");

            migrationBuilder.DropTable(
                name: "Workflows");
        }
    }
}
