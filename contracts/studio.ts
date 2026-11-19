/** Public schema v2. Keys prefixed with _ and signed provider results stay server-side. */
export type ProjectStatus = 'draft' | 'generating' | 'ready_for_review' | 'confirmed';
export type TaskKind = 'image-generation' | 'img-to-3d' | 'multimodal-to-image'; // last: legacy, read only
export type TaskStatus = 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled';
export type AssetFormat = 'glb' | 'png' | 'jpg' | 'webp' | 'zip' | 'ply';
export type VersionState = 'designing' | 'awaiting_design_confirmation' | 'generating' | 'ready' | 'confirmed' | 'failed' | 'discarded';

export interface AssetFile {
  id: string;
  format: AssetFormat;
  fileName: string;
  sizeBytes: number;
  storageKey: string;
  /** Durable app-owned URL. No expiring provider URL in the public schema. */
  url: string;
  sha256: string;
  /** Present on every newly generated model. Legacy raw-photo models may lack these. */
  sourceDesignImageId?: string;
  sourceDesignVersionId?: string;
  sourceObjectVersionId?: string;
  stage?: 'object' | 'anthro';
}
export interface SourceImage extends AssetFile {
  format: 'jpg' | 'png' | 'webp';
  origin: 'upload' | 'generated-design' | 'model-render';
  role: 'primary' | 'reference' | 'auxiliary';
  order: number;
  purpose: string;
  mimeType: 'image/jpeg' | 'image/png' | 'image/webp';
  width: number;
  height: number;
  createdAt: string;
  createdFromTaskId?: string;
}
export interface ImageReference {
  imageId: string;
  /** Snapshot of purpose at submission. Array position defines image numbering. */
  purpose: string;
}
export interface CostEstimate {
  currency: 'CNY' | 'CREDITS';
  amount: number;
  isEstimate: true;
  basis: 'estimate' | 'usage';
  inputImageCount?: number;
  outputImageCount?: number;
  inputUnitPriceCny?: number;
  outputUnitPriceCny?: number;
  inputTokens?: number;
  outputTokens?: number;
}
export interface RequestRecord {
  method: 'POST';
  endpoint: string;
  model: string;
  prompt?: string;
  negative_prompt?: string;
  n?: 1;
  size?: '1024x1024';
  prompt_extend?: false;
  watermark?: false;
  /** Durable input records stand in for the data URLs in the actual HTTP payload. */
  inputImages?: { imageId: string; sha256: string; encoding: 'data-url' }[];
  inputImageIds?: string[];
  parameters?: { faceCount: number; enablePbr: boolean; outputFormat: string[] };
}
export interface GenerationTask {
  id: string;
  kind: TaskKind;
  stageKind?: 'object' | 'anthro';
  provider: 'qwen-image' | 'aholo-lux3d';
  providerVersion?: string; // absent only in some legacy records
  providerRegion?: 'cn' | 'com';
  providerRequestId?: string | null;
  providerTaskId?: string;
  previousProviderTaskIds?: string[];
  clientRequestId?: string;
  versionId: string;
  inputImageIds: string[];
  references?: ImageReference[];
  sourceDesignImageId?: string;
  sourceObjectVersionId?: string;
  dependsOnTaskIds: string[];
  userInstruction?: string;
  prompt?: string | null;
  negativePrompt?: string;
  promptSettingsSnapshot?: Omit<PromptSettings, 'agent'>;
  requestRecord?: RequestRecord;
  cost?: CostEstimate;
  /** Recorded image and internal inspection estimates, including partial failures. */
  costRecorded?: boolean;
  costIncomplete?: boolean;
  /** Candidate needs user inspection; internal decisions and attempts stay private. */
  needsReview?: boolean;
  usage?: Record<string, string | number>;
  responseStatus?: number;
  providerErrorCode?: string;
  /** Download recovery deadline; the copied local result itself does not expire. */
  providerResultExpiresAt?: string;
  status: TaskStatus;
  stage: 'queued' | 'uploading' | 'submitting' | 'polling' | 'downloading' | 'checking' | 'complete';
  providerStatus?: number;
  canRetry?: boolean;
  outputs?: AssetFile[];
  error?: { code: string; message: string };
  createdAt: string;
  updatedAt: string;
}
export interface AssetVersion {
  id: string;
  version: number;
  stage?: 'object' | 'anthro';
  stageName?: 'juvenile' | 'mature' | 'final';
  sourceObjectVersionId?: string;
  sourceObjectDesignImageId?: string;
  conceptOption?: 'handle-ear' | 'handle-tail' | 'handle-equipment' | 'custom';
  rigStatus?: 'not-tested' | 'pending' | 'passed' | 'failed';
  animationStatus?: 'not-tested' | 'pending' | 'passed' | 'failed';
  parentVersionId?: string;
  sourceImageIds: string[];
  references?: ImageReference[];
  userInstruction: string;
  actualPrompt?: string;
  artRulesVersion?: string;
  promptSettingsRevision?: number;
  negativePrompt?: string;
  /** Stable design task ID; generating 3D never overwrites this. */
  createdFromTaskId: string;
  designTaskId?: string;
  designImageId?: string;
  modelTaskId?: string;
  model?: AssetFile;
  previewImage?: AssetFile;
  state: VersionState;
  legacyAsset?: boolean;
  createdAt: string;
}
export interface CreationProject {
  schemaVersion: 2;
  id: string;
  title: string;
  status: ProjectStatus;
  /** Uploads and user-visible design versions; internal candidates stay server-side. */
  sourceImages: SourceImage[];
  /** Selected initial references, in order; maximum three. */
  activeImageIds: string[];
  inputInstruction: string;
  /** Explicitly restored/current design selection, independent of confirmed model. */
  activeVersionId: string | null;
  activeAnthroVersionId?: string | null;
  confirmedVersionId?: string;
  confirmedAnthroVersionId?: string;
  versions: AssetVersion[];
  tasks: GenerationTask[];
  createdAt: string;
  updatedAt: string;
}
export interface GenerateDesignRequest {
  requestId: string;
  instruction: string;
  references: ImageReference[]; // 0–3; text required if empty
}
export interface ReviseDesignRequest {
  requestId: string;
  baseVersionId: string;
  instruction: string;
  auxiliaryReferences: ImageReference[]; // 0–2; selected design is always image 1
}

/** Global prompt revisions are persisted; each image task snapshots the applied revision. */
export interface PromptSettings {
  revision: number; // 0: built-in recommendation; saved versions start at 1
  baseVersion: string;
  updatedAt: string | null;
  fields: {
    style: string;
    composition: string;
    textDesign: string;
    referenceDesign: string;
    revision: string;
    defaultIdea: string;
    negative: string;
  };
  anthro: {
    style: string;
    composition: string;
    base: string;
    revision: string;
    defaultIdea: string;
    negative: string;
  };
  /** Configuration-page only; omitted from public task snapshots. */
  agent: AgentSettings;
}
export interface AgentSettings {
  model: string;
  apiUrl: string;
  maxCorrections: number; // 0–5, default 2 beyond first generation
  timeoutSeconds: number;
  inputCnyPerMillion: number;
  outputCnyPerMillion: number;
  instruction: string;
  repairInstruction: string;
}
export interface SavePromptSettingsRequest {
  expectedRevision: number;
  fields: PromptSettings['fields'];
  anthro?: PromptSettings['anthro'];
  /** Omission preserves the current Agent configuration for older clients. */
  agent?: AgentSettings;
}
