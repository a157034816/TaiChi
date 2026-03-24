import { z } from "zod";

const colorHexSchema = z.string().regex(/^#[0-9a-f]{6}$/i, 'Expected a hex color like "#RRGGBB".');
const dateValueSchema = z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Expected a date like "2026-03-21".');

const runtimeCapabilitiesSchema = z.object({
  canExecute: z.boolean().optional(),
  canDebug: z.boolean().optional(),
  canProfile: z.boolean().optional(),
});

const libraryPortDefinitionSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1),
  dataType: z.string().min(1).optional(),
});

const storedPortDefinitionSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1),
  dataType: z.string().min(1).optional(),
});

export const nodePortDefinitionSchema = storedPortDefinitionSchema;

export const typeMappingEntrySchema = z.object({
  canonicalId: z.string().min(1),
  type: z.string().min(1),
  color: colorHexSchema.optional(),
});

const nodeLibraryFieldBaseSchema = z.object({
  key: z.string().min(1),
  label: z.string().min(1),
  placeholder: z.string().min(1).optional(),
});

const nodeLibraryStringFieldSchema = nodeLibraryFieldBaseSchema.extend({
  kind: z.enum(["text", "textarea", "decimal"]),
  defaultValue: z.string().optional(),
});

const nodeLibraryDateFieldSchema = nodeLibraryFieldBaseSchema.extend({
  kind: z.literal("date"),
  defaultValue: dateValueSchema.optional(),
});

const nodeLibraryColorFieldSchema = nodeLibraryFieldBaseSchema.extend({
  kind: z.literal("color"),
  defaultValue: colorHexSchema.optional(),
});

const nodeLibraryBooleanFieldSchema = nodeLibraryFieldBaseSchema.extend({
  kind: z.literal("boolean"),
  defaultValue: z.boolean().optional(),
});

const nodeLibraryNumericFieldSchema = nodeLibraryFieldBaseSchema.extend({
  kind: z.enum(["int", "float", "double"]),
  defaultValue: z.number().optional(),
});

const nodeLibrarySelectFieldSchema = nodeLibraryFieldBaseSchema.extend({
  kind: z.literal("select"),
  optionsEndpoint: z.string().url(),
  defaultValue: z.string().optional(),
});

export const nodeLibraryFieldSchema = z.discriminatedUnion("kind", [
  nodeLibraryStringFieldSchema,
  nodeLibraryDateFieldSchema,
  nodeLibraryColorFieldSchema,
  nodeLibraryBooleanFieldSchema,
  nodeLibraryNumericFieldSchema,
  nodeLibrarySelectFieldSchema,
]);

export const nodeAppearanceSchema = z.object({
  bgColor: z.string().optional(),
  borderColor: z.string().optional(),
  textColor: z.string().optional(),
});

export const invalidTemplateMarkerSchema = z.object({
  code: z.string().min(1),
  reason: z.string().min(1),
});

export const nodeLibraryItemSchema = z.object({
  type: z.string().min(1),
  displayName: z.string().min(1),
  description: z.string().min(1).optional(),
  category: z.string().min(1),
  inputs: z.array(libraryPortDefinitionSchema).optional(),
  outputs: z.array(libraryPortDefinitionSchema).optional(),
  fields: z.array(nodeLibraryFieldSchema).optional(),
  defaultData: z.record(z.string(), z.unknown()).optional(),
  appearance: nodeAppearanceSchema.optional(),
});

export const nodeGraphNodeDataSchema = z.object({
  label: z.string().min(1),
  labelOverride: z.string().optional(),
  description: z.string().optional(),
  descriptionOverride: z.string().optional(),
  category: z.string().min(1).optional(),
  nodeType: z.string().min(1),
  inputs: z.array(storedPortDefinitionSchema).optional(),
  outputs: z.array(storedPortDefinitionSchema).optional(),
  values: z.record(z.string(), z.unknown()).optional(),
  appearance: nodeAppearanceSchema.optional(),
  templateMarkers: z.array(invalidTemplateMarkerSchema).optional(),
});

export const nodeGraphNodeSchema = z.object({
  id: z.string().min(1),
  type: z.string().min(1),
  position: z.object({
    x: z.number(),
    y: z.number(),
  }),
  data: nodeGraphNodeDataSchema,
  width: z.number().optional(),
  height: z.number().optional(),
  style: z.record(z.string(), z.unknown()).optional(),
});

export const nodeGraphEdgeSchema = z.object({
  id: z.string().min(1),
  source: z.string().min(1),
  target: z.string().min(1),
  sourceHandle: z.string().min(1).nullable().optional(),
  targetHandle: z.string().min(1).nullable().optional(),
  label: z.string().optional(),
  type: z.string().optional(),
  animated: z.boolean().optional(),
  invalidReason: z.string().optional(),
});

export const nodeGraphDocumentSchema = z.object({
  graphId: z.string().optional(),
  name: z.string().min(1),
  description: z.string().optional(),
  nodes: z.array(nodeGraphNodeSchema),
  edges: z.array(nodeGraphEdgeSchema),
  viewport: z.object({
    x: z.number(),
    y: z.number(),
    zoom: z.number(),
  }),
});

export const createSessionRequestSchema = z.object({
  runtimeId: z.string().min(1),
  completionWebhook: z.string().url(),
  graph: nodeGraphDocumentSchema.optional(),
  metadata: z.record(z.string(), z.string()).optional(),
});

export const runtimeRegistrationRequestSchema = z.object({
  runtimeId: z.string().min(1),
  domain: z.string().min(1),
  clientName: z.string().optional(),
  controlBaseUrl: z.string().url(),
  libraryVersion: z.string().min(1),
  capabilities: runtimeCapabilitiesSchema.optional(),
  library: z.object({
    nodes: z.array(nodeLibraryItemSchema),
    typeMappings: z.array(typeMappingEntrySchema).optional(),
  }),
});

export const runtimeLibraryResponseSchema = z.object({
  libraryVersion: z.string().min(1).optional(),
  library: z.object({
    nodes: z.array(nodeLibraryItemSchema),
    typeMappings: z.array(typeMappingEntrySchema).optional(),
  }),
});

export const completeSessionRequestSchema = z.object({
  graph: nodeGraphDocumentSchema,
});

export const refreshRuntimeLibraryRequestSchema = z.object({
  graph: nodeGraphDocumentSchema.optional(),
});

export const debugSessionSnapshotSchema = z.object({
  status: z.enum(["idle", "running", "paused", "completed", "budget_exceeded", "failed"]),
  pauseReason: z.string().nullable(),
  pendingNodeId: z.string().nullable(),
  lastError: z.unknown().nullable().optional(),
  lastEvent: z.record(z.string(), z.unknown()).nullable().optional(),
  profiler: z.record(z.string(), z.record(z.string(), z.number())),
  results: z.record(z.string(), z.array(z.unknown())),
  events: z.array(z.record(z.string(), z.unknown())),
});

export const createDebugSessionRequestSchema = z.object({
  graph: nodeGraphDocumentSchema,
  breakpoints: z.array(z.string().min(1)).optional(),
});

export const updateDebugBreakpointsRequestSchema = z.object({
  breakpoints: z.array(z.string().min(1)),
});

export const debugSessionPayloadSchema = z.object({
  debugSessionId: z.string().min(1),
  graph: nodeGraphDocumentSchema,
  breakpoints: z.array(z.string().min(1)),
  snapshot: debugSessionSnapshotSchema,
});

export const nodeLibraryEnvelopeSchema = z.object({
  nodes: z.array(nodeLibraryItemSchema),
  typeMappings: z.array(typeMappingEntrySchema).optional(),
});

export const nodeFieldOptionsResponseSchema = z.object({
  options: z.array(
    z.object({
      value: z.string(),
      label: z.string(),
    }),
  ),
});
