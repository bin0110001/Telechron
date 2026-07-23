export interface WorkflowRunItem {
  id: string;
  workflowName: string;
  projectName: string;
  status: 'Passed' | 'Running' | 'AwaitingApproval' | 'Failed' | 'PartiallyFailed';
  startedAt: string;
  duration: string;
}

export interface ProjectItem {
  id: string;
  name: string;
  repoPath: string;
  activeDesignDoc: string;
  activeFindingsCount: number;
}

export interface MachineItem {
  id: string;
  name: string;
  status: 'Online' | 'Busy' | 'Offline';
  activeAgents: number;
  gpuLocks: string;
}

export interface ModuleItem {
  id: string;
  name: string;
  kind: 'Toolchain' | 'Runner' | 'FunctionExecutor' | 'Connector' | 'LlmEngine';
  version: string;
  status: 'Loaded' | 'SelfTested';
}

export interface ApprovalItem {
  id: string;
  projectName: string;
  gateId: string;
  prompt: string;
  requestedBy: string;
  createdAt: string;
}

export interface DesignDocRequirementItem {
  id: string;
  requirementId: string;
  title: string;
  statement: string;
  status: 'Active' | 'UnderRevision' | 'DriftDetected';
}

export interface AuditLogItem {
  id: string;
  timestamp: string;
  traceId: string;
  action: string;
  userOrAgent: string;
}
