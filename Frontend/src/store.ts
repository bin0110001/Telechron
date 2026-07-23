import type {
  WorkflowRunItem,
  ProjectItem,
  MachineItem,
  ModuleItem,
  ApprovalItem,
  DesignDocRequirementItem,
  AuditLogItem
} from './types';

export const mockRuns: WorkflowRunItem[] = [
  { id: 'run_8f9a2b1c', workflowName: 'Full Core Fix Pipeline', projectName: 'Telechron Core', status: 'Passed', startedAt: '2 mins ago', duration: '14s' },
  { id: 'run_3d4e5f6a', workflowName: 'Capability Synthesis & Verification', projectName: 'Telechron Core', status: 'AwaitingApproval', startedAt: 'Just now', duration: '3s' },
  { id: 'run_1a2b3c4d', workflowName: 'Scheduled Dependency Audit', projectName: 'DotnetToolchain', status: 'Passed', startedAt: '12 mins ago', duration: '22s' },
];

export const mockProjects: ProjectItem[] = [
  { id: 'proj_telechron_self', name: 'Telechron Core (Reflexive)', repoPath: 'c:\\Projects\\Telechron', activeDesignDoc: 'Telechron System Design v1.0', activeFindingsCount: 0 },
  { id: 'proj_artificers_vault', name: 'Artificers Vault Store', repoPath: 'c:\\Projects\\ArtificersVault', activeDesignDoc: 'Vault E-Commerce Specs', activeFindingsCount: 1 },
];

export const mockMachines: MachineItem[] = [
  { id: 'mach_local_win11', name: 'Local Master (Win11)', status: 'Online', activeAgents: 4, gpuLocks: 'NVIDIA RTX 4090 (Reserved)' },
  { id: 'mach_agent_worker_1', name: 'Container Worker 01', status: 'Busy', activeAgents: 2, gpuLocks: 'None' },
];

export const mockModules: ModuleItem[] = [
  { id: 'mod_dotnet_toolchain', name: 'DotnetToolchainModule', kind: 'Toolchain', version: '1.0.0', status: 'Loaded' },
  { id: 'mod_dotnet_test_runner', name: 'DotnetTestRunnerModule', kind: 'Runner', version: '1.0.0', status: 'Loaded' },
  { id: 'mod_core_functions', name: 'CoreFunctionsModule', kind: 'FunctionExecutor', version: '1.0.0', status: 'SelfTested' },
  { id: 'mod_ollama_engine', name: 'OllamaEngineModule', kind: 'LlmEngine', version: '1.0.0', status: 'Loaded' },
  { id: 'mod_github_connector', name: 'GitHubConnectorModule', kind: 'Connector', version: '1.0.0', status: 'Loaded' },
];

export const mockApprovals: ApprovalItem[] = [
  { id: 'app_req_99f', projectName: 'Telechron Core', gateId: 'R-SEC4-privileged-path', prompt: 'Capability synthesis detected missing Ollama connector capability. Synthesize and test in isolated container?', requestedBy: 'CapabilityGapApprovalFlow', createdAt: '3 mins ago' },
  { id: 'app_req_88a', projectName: 'Telechron Core', gateId: 'R-DM16b-requirement-revision', prompt: 'Revise requirement R-FIX13 to tighten architectural drift threshold to 0.05?', requestedBy: 'DesignDocumentManager', createdAt: '10 mins ago' },
];

export const mockRequirements: DesignDocRequirementItem[] = [
  { id: 'req_1', requirementId: 'R-NS1', title: 'Persona Isolation Boundary', statement: 'Every managed Project operates strictly within its designated Persona security sandbox.', status: 'Active' },
  { id: 'req_2', requirementId: 'R-FIX13', title: 'Architectural Drift Detection', statement: 'Repair patches must pass drift checks against active design doc invariants.', status: 'UnderRevision' },
  { id: 'req_3', requirementId: 'R-SEC4', title: 'Privileged Path Approvals', statement: 'Modifications to repair pipeline or core framework require explicit human approval.', status: 'Active' },
];

export const mockAuditLogs: AuditLogItem[] = [
  { id: 'log_001', timestamp: '2026-07-23 15:42:01', traceId: 'tr_9f8a37b4e21', action: 'ApprovalRequested', userOrAgent: 'WorkflowEngine' },
  { id: 'log_002', timestamp: '2026-07-23 15:40:12', traceId: 'tr_4a2b1c8e9f0', action: 'WorkflowRunStarted', userOrAgent: 'SchedulerService' },
  { id: 'log_003', timestamp: '2026-07-23 15:35:00', traceId: 'tr_7e6f5d4c3b2', action: 'TelemetryBatchFlushed', userOrAgent: 'TelemetryBatcher' },
];
