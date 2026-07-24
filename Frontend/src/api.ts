// Real fetch-based client against the Host's REST API (R-UI2/R-SEC6).
// JWT is kept in memory + sessionStorage (not localStorage, so a closed
// tab doesn't leave a long-lived token sitting in persistent storage).

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5280';
const TOKEN_STORAGE_KEY = 'telechron_access_token';

let accessToken: string | null = sessionStorage.getItem(TOKEN_STORAGE_KEY);

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
  if (token) {
    sessionStorage.setItem(TOKEN_STORAGE_KEY, token);
  } else {
    sessionStorage.removeItem(TOKEN_STORAGE_KEY);
  }
}

export function isAuthenticated(): boolean {
  return accessToken !== null;
}

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set('Content-Type', 'application/json');
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers });

  if (response.status === 401) {
    setAccessToken(null);
    throw new ApiError(401, 'Session expired. Please log in again.');
  }
  if (!response.ok) {
    const text = await response.text().catch(() => '');
    throw new ApiError(response.status, text || `Request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  const result = await request<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });
  setAccessToken(result.accessToken);
  return result;
}

export interface SetupStatusResponse {
  isSetupComplete: boolean;
}

export interface CreateFirstAdminResponse {
  userId: string;
  email: string;
}

// R-SEC6: the one-time bootstrap path -- see Host/Controllers/SetupController.cs.
// Unauthenticated by design (nobody can be authenticated yet the first
// time this legitimately needs to run), so these two calls don't go
// through the shared request() helper's 401-handling (there's no session
// to expire).
export async function getSetupStatus(): Promise<SetupStatusResponse> {
  const response = await fetch(`${API_BASE}/api/setup/status`);
  if (!response.ok) throw new ApiError(response.status, 'Failed to check setup status.');
  return response.json();
}

export async function createFirstAdmin(
  setupToken: string, email: string, password: string, displayName: string,
): Promise<CreateFirstAdminResponse> {
  const response = await fetch(`${API_BASE}/api/setup/first-admin`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ setupToken, email, password, displayName }),
  });
  if (!response.ok) {
    const text = await response.text().catch(() => '');
    throw new ApiError(response.status, text || `Setup failed: ${response.status}`);
  }
  return response.json();
}

export function logout(): void {
  setAccessToken(null);
}

// --- Domain response shapes (mirror the Host controller response records) ---

export interface ProjectResponse {
  id: string;
  name: string;
  rootPath: string;
  repairPolicy: 'RequireApproval' | 'FullyAutonomous' | number;
  toolchainId: string | null;
  llmConnectionId: string | null;
  createdAtUtc: string;
}

export interface RunResponse {
  id: string;
  projectId: string;
  machineId: string | null;
  status: number | string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  lastHeartbeatUtc: string | null;
}

export interface ApprovalResponse {
  id: string;
  workflowRunId: string;
  stepId: string;
  gateId: string;
  prompt: string;
  isSatisfied: boolean;
  approvedByUserId: string | null;
  approverComment: string | null;
  createdAtUtc: string;
  decisionAtUtc: string | null;
}

export interface RequirementResponse {
  id: string;
  requirementId: string;
  title: string;
  body: string;
  status: 'Active' | 'Superseded' | 'Deprecated' | number;
  currentRevisionNumber: number;
}

export interface DesignDocumentResponse {
  id: string;
  projectId: string;
  title: string;
  createdAtUtc: string;
  requirements: RequirementResponse[];
}

export interface MachineResponse {
  id: string;
  name: string;
  hostname: string;
  isOnline: boolean;
  registeredAtUtc: string;
  lastHeartbeatUtc: string | null;
}

export interface ModuleResponse {
  id: string;
  name: string;
  kind: string;
  version: string;
  capabilities: string[];
  installedAtUtc: string;
}

export interface ScheduleResponse {
  id: string;
  workflowId: string;
  projectId: string;
  machineId: string | null;
  cronExpression: string;
  isEnabled: boolean;
  serializePerMachine: boolean;
  serializePerProject: boolean;
  createdAtUtc: string;
  lastFiredAtUtc: string | null;
}

export interface SecretResponse {
  id: string;
  projectId: string;
  handle: string;
  name: string;
  createdAtUtc: string;
  revokedAtUtc: string | null;
}

export interface LlmConnectionResponse {
  id: string;
  name: string;
  provider: string;
  secretHandle: string | null;
  createdAtUtc: string;
}

export interface LlmCallResponse {
  id: string;
  llmConnectionId: string;
  projectId: string | null;
  provider: string;
  model: string;
  promptTokens: number;
  completionTokens: number;
  estimatedCostUsd: number;
  succeeded: boolean;
  occurredAtUtc: string;
}

export interface AuditEventResponse {
  sequence: number;
  kind: number | string;
  occurredAtUtc: string;
  actorUserId: string | null;
  projectId: string | null;
  detailJson: string;
}

export interface AuditChainVerificationResponse {
  isIntact: boolean;
  firstTamperedSequence: number | null;
}

export interface RepairAttemptResponse {
  id: string;
  findingIds: string[];
  snapshotRef: string;
  approvalDecision: number | string | null;
  approverUserId: string | null;
  commitReference: string | null;
  provenanceRecordJson: string | null;
  createdAtUtc: string;
}

export interface PendingRepairDiffResponse {
  id: string;
  findingIds: string[];
  snapshotRef: string;
  patchDiff: string;
  createdAtUtc: string;
}

export interface WorkflowResponse {
  id: string;
  projectId: string;
  name: string;
  definitionJson: string;
  failurePolicy: number | string;
  createdAtUtc: string;
}

export const apiClient = {
  listProjects: () => request<ProjectResponse[]>('/api/projects'),
  getProject: (projectId: string) => request<ProjectResponse>(`/api/projects/${projectId}`),

  listRuns: (projectId: string) => request<RunResponse[]>(`/api/runs?projectId=${encodeURIComponent(projectId)}`),

  listPendingApprovals: () => request<ApprovalResponse[]>('/api/approvals/pending'),
  submitApprovalDecision: (requestId: string, approve: boolean, comment?: string) =>
    request<ApprovalResponse>(`/api/approvals/${requestId}/decision`, {
      method: 'POST',
      body: JSON.stringify({ approve, comment: comment ?? null, parameterOverridesJson: null }),
    }),

  getDesignDocument: (projectId: string) =>
    request<DesignDocumentResponse | null>(`/api/projects/${projectId}/design-document`, undefined).catch((err) => {
      if (err instanceof ApiError && err.status === 404) return null;
      throw err;
    }),

  listMachines: () => request<MachineResponse[]>('/api/machines'),
  listModules: () => request<ModuleResponse[]>('/api/modules'),

  listSchedules: (projectId: string) => request<ScheduleResponse[]>(`/api/projects/${projectId}/schedules`),

  listSecrets: (projectId: string) => request<SecretResponse[]>(`/api/projects/${projectId}/secrets`),

  listLlmConnections: () => request<LlmConnectionResponse[]>('/api/llm/connections'),
  listLlmCalls: (projectId: string, lookbackDays = 30) =>
    request<LlmCallResponse[]>(`/api/llm/calls?projectId=${encodeURIComponent(projectId)}&lookbackDays=${lookbackDays}`),

  listAuditEvents: (fromSequence = 0, limit = 100) =>
    request<AuditEventResponse[]>(`/api/audit-log?fromSequence=${fromSequence}&limit=${limit}`),
  verifyAuditChain: () => request<AuditChainVerificationResponse>('/api/audit-log/verify'),

  listRepairAttempts: (projectId: string) => request<RepairAttemptResponse[]>(`/api/projects/${projectId}/repair-attempts`),

  listPendingRepairDiffs: (projectId: string) =>
    request<PendingRepairDiffResponse[]>(`/api/projects/${projectId}/pending-repair-diffs`),

  listWorkflows: (projectId: string) => request<WorkflowResponse[]>(`/api/projects/${projectId}/workflows`),
};
