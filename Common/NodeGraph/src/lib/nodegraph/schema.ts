import { z } from "zod";

const nodeFieldKindSchema = z.enum(["text", "textarea", "number", "boolean"]);

export const nodePortDefinitionSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1),
  dataType: z.string().min(1).optional(),
});

export const nodeLibraryFieldSchema = z.object({
  key: z.string().min(1),
  label: z.string().min(1),
  kind: nodeFieldKindSchema,
  placeholder: z.string().optional(),
  defaultValue: z.union([z.string(), z.number(), z.boolean()]).optional(),
});

export const nodeAppearanceSchema = z.object({
  bgColor: z.string().optional(),
  borderColor: z.string().optional(),
  textColor: z.string().optional(),
});

export const nodeLibraryItemSchema = z.object({
  type: z.string().min(1),
  label: z.string().min(1),
  description: z.string().default(""),
  category: z.string().min(1),
  inputs: z.array(nodePortDefinitionSchema).optional(),
  outputs: z.array(nodePortDefinitionSchema).optional(),
  fields: z.array(nodeLibraryFieldSchema).optional(),
  defaultData: z.record(z.string(), z.unknown()).optional(),
  appearance: nodeAppearanceSchema.optional(),
});

export const nodeGraphNodeDataSchema = z.object({
  label: z.string().min(1),
  description: z.string().optional(),
  category: z.string().optional(),
  nodeType: z.string().min(1),
  inputs: z.array(nodePortDefinitionSchema).optional(),
  outputs: z.array(nodePortDefinitionSchema).optional(),
  values: z.record(z.string(), z.unknown()).optional(),
  appearance: nodeAppearanceSchema.optional(),
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
  domain: z.string().min(1),
  clientName: z.string().optional(),
  nodeLibraryEndpoint: z.string().url(),
  completionWebhook: z.string().url(),
  graph: nodeGraphDocumentSchema.optional(),
  metadata: z.record(z.string(), z.string()).optional(),
});

export const completeSessionRequestSchema = z.object({
  graph: nodeGraphDocumentSchema,
});

export const nodeLibraryEnvelopeSchema = z.union([
  z.object({
    nodes: z.array(nodeLibraryItemSchema),
  }),
  z.array(nodeLibraryItemSchema),
]);
