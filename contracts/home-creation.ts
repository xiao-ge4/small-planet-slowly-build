/** Planning contracts and local Unity acceptance records; not a public game runtime API. */
export type StageId = 'brief' | 'concept' | 'breakdown' | 'asset_design' | 'assets' | 'layout' | 'validation' | 'finalize';
export interface ArtifactRef { id: string; version: number; path: string; sha256: string }
export type MoneyUnit = 'USD' | 'CNY' | 'AHOLO_CREDITS';
export interface Cost { unit: MoneyUnit; amount: number | null; basis: 'estimate' | 'settled' | 'unknown'; sourceRef?: ArtifactRef }
export interface PlotGrant {
  plotId: string; adjacentPlotId: string; profile: 'starter'; terrainTemplateRef: ArtifactRef;
  /** Game supplies both anchors; Agent must not invent or move them. */
  connectionAnchors: { localPosition: [number, number, number]; forward: [number, number, number]; clearWidth: number }[];
}
export interface Brief {
  idea: string; theme: string; mustHave: string[]; mustAvoid: string[];
  references: { imageRef: ArtifactRef; purpose: string; order: number }[];
  interactionKinds: ('Water' | 'Chest')[]; plotGrant: PlotGrant; boundaryRef: ArtifactRef;
  /** null is unset, never unlimited. Unknown-price authorization is explicit per operation. */
  budgetCaps: Record<MoneyUnit, number | null>;
}
export interface ImageReview {
  pass: boolean; issues: string[]; correctionCount: number;
  action: 'accept' | 'edit_candidate' | 'regenerate_from_originals' | 'ask_player';
  selectedInputRefs: ArtifactRef[]; revisionPrompt?: string;
}
export interface Concept {
  versionId: string; imageRef: ArtifactRef; sourceRefs: ArtifactRef[];
  actualPrompt: string; providerModel: string; requestRecordRef: ArtifactRef; review: ImageReview;
}
export interface AssetPlanItem {
  assetId: string; name: string;
  route: 'reuse' | 'material_variant' | 'generated_mesh' | 'generated_texture' | 'procedural';
  rationale: string; existingAssetRef?: ArtifactRef; sourceConceptRef: ArtifactRef;
  separate: boolean; interactionKind?: 'Water' | 'Chest'; count: number;
  colliderRecipe: 'none' | 'box' | 'capsule' | 'ground_mesh' | 'no_walk_volume';
  expectedSizeMetres: [number, number, number];
}
export interface AssetPlan {
  conceptRef: ArtifactRef; assets: AssetPlanItem[]; keepInBase: string[]; separateStatefulObjects: string[];
  placementZones: { id: string; purpose: string; footprint: [number, number][] }[];
  reservedConnection: PlotGrant['connectionAnchors']; estimatesByCurrency: Cost[];
}
export interface MeshSettings {
  provider: 'aholo-lux3d'; model: 'G1'; faceCount: number; enablePbr: boolean; format: 'glb';
}
export interface AssetDesign {
  assetId: string; route: AssetPlanItem['route']; sourceConceptRef: ArtifactRef;
  designRefs: ArtifactRef[]; orderedViews: { view: 'front' | 'right' | 'back'; imageRef: ArtifactRef }[];
  heightMetres: number; review: ImageReview; modelSettings?: MeshSettings; estimate: Cost[];
}
export interface ProductionBatch {
  batchId: string; briefRef: ArtifactRef; conceptRef: ArtifactRef; assetPlanRef: ArtifactRef;
  designRefs: ArtifactRef[]; modelSettings: MeshSettings; estimate: Cost[];
  budgetCaps: Brief['budgetCaps'];
  /** Explicit list; a null estimate alone does not permit spending. */
  unknownCostAuthorizations: { operationId: string; playerConfirmationId: string }[];
}
export interface TaskRecord {
  operationId: string; requestHash: string; stage: StageId; assetId?: string;
  status: 'planned' | 'submitting' | 'submitted' | 'running' | 'downloaded' | 'verified' | 'failed' | 'submission_unknown' | 'cancel_requested' | 'cancelled';
  providerTaskId?: string; model: string; inputRefs: ArtifactRef[];
  promptRef?: ArtifactRef; parameterRef: ArtifactRef; resultRefs: ArtifactRef[];
  estimate: Cost[]; actual: Cost[]; errorCode?: string;
}
export interface LayoutInstance {
  instanceId: string; assetRef: ArtifactRef; parentInstanceId?: string;
  position: [number, number, number]; rotationEuler: [number, number, number]; scale: [number, number, number];
  /** Stable ID namespaced by plotId. Never name objects solely by model filename. */
  interaction?: { stableId: string; kind: 'Water' | 'Chest'; approach: [number, number, number]; stateKey: string };
}
export interface Layout {
  plotId: string; terrainTemplateRef: ArtifactRef; connectionAnchors: PlotGrant['connectionAnchors'];
  instances: LayoutInstance[]; environmentRef: ArtifactRef; parentLayoutRef?: ArtifactRef;
}
export interface PreparedAsset {
  assetId: string; sourceRefs: ArtifactRef[]; localFiles: ArtifactRef[];
  /** SourceDesign/task refs are required for generated assets; existing provenance for reuse. */
  sourceDesignRef?: ArtifactRef; taskRef?: ArtifactRef;
  unitScale: number; upAxis: 'Y'; pivot: [number, number, number];
  bounds: { min: [number, number, number]; max: [number, number, number] };
  lods: { level: number; modelRef: ArtifactRef; triangles: number }[];
  colliderRecipe: AssetPlanItem['colliderRecipe'];
  sockets: { name: string; position: [number, number, number] }[];
  visualReview: { passed: boolean; issues: string[]; previewRefs: ArtifactRef[] };
}
export interface ValidationReport {
  layoutRef: ArtifactRef; sceneHash: string;
  checks: { name: string; passed: boolean; evidenceRefs: ArtifactRef[] }[];
  runtimeErrors: number; screenshots: ArtifactRef[]; saveReloadResult: boolean; limitations: string[];
}
export interface PlayerConfirmation {
  id: string; gate: 'concept' | 'production_batch' | 'final_layout'; actor: 'player';
  decision: 'approved' | 'rejected'; at: string; boundRefs: ArtifactRef[]; parametersHash?: string;
}
export interface StageRecord {
  stage: StageId; status: 'not_started' | 'running' | 'awaiting_player' | 'complete' | 'failed' | 'stale';
  inputs: ArtifactRef[]; outputs: ArtifactRef[]; confirmationId?: string; issues: string[];
}
export interface CreationRun {
  schemaVersion: 1; runId: string; mode: 'draft' | 'live'; pipelineRef: ArtifactRef; boundaryRef: ArtifactRef;
  stages: StageRecord[]; confirmations: PlayerConfirmation[]; tasks: TaskRecord[];
}
export interface SceneRelease {
  plotId: string; releaseId: string; layoutRef: ArtifactRef; validationRef: ArtifactRef;
  assetRefs: ArtifactRef[]; playerApproval: PlayerConfirmation;
  previousReleaseRef: ArtifactRef | null; historyRef: ArtifactRef;
}

/** Implemented local acceptance wire format; distinct from a future published SceneRelease. */
export interface LocalRuntimeReport {
  bindingVersion: 1; runId: string; revision: number; assemblyVersion: string;
  scenePath: string; sceneSha256: string; layoutRefId: string; layoutJson: string;
  dependencyFiles: { path: string; sha256: string }[];
  passed: boolean; errors: number; checks: { name: string; passed: boolean; detail: string }[];
  utc?: string; unity?: string; isolatedSave?: string;
}
export interface RuntimeValidationJob {
  id: string; layoutRefId: string; state: 'running' | 'complete' | 'failed';
  startedAt: string; finishedAt?: string; message?: string;
}
export interface LocalSceneSnapshot {
  schemaVersion: 1; scope: 'local-scene-acceptance'; releaseId: string; runId: string;
  revision: number; assemblyVersion: string; confirmedAt: string;
  layoutRef: ArtifactRef; sceneRef: ArtifactRef; validationRef: ArtifactRef;
  previewRefs: ArtifactRef[]; screenshotRefs: ArtifactRef[]; assetManifestRef?: ArtifactRef;
  playerApproval: { id: string; gate: 'final_layout'; actor: 'player'; at: string; bindingHash: string; visualAccepted: true };
}
