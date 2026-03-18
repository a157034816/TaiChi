"use client";

import { FilePenLine, Settings2 } from "lucide-react";

import {
  buildEdgeInspectorDetails,
  getInspectorAccent,
  getInspectorSelectionTabLabel,
} from "@/lib/nodegraph/inspector";
import type { NodeGraphEdge, NodeLibraryItem, NodeGraphNode } from "@/lib/nodegraph/types";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";

interface NodeInspectorPanelProps {
  edge: NodeGraphEdge | null;
  node: NodeGraphNode | null;
  nodes: NodeGraphNode[];
  template: NodeLibraryItem | null;
  graphName: string;
  graphDescription: string;
  onGraphNameChange: (value: string) => void;
  onGraphDescriptionChange: (value: string) => void;
  onNodeFieldChange: (field: "label" | "description", value: string) => void;
  onNodeValueChange: (key: string, value: string | number | boolean) => void;
}

export function NodeInspectorPanel({
  edge,
  node,
  nodes,
  template,
  graphName,
  graphDescription,
  onGraphNameChange,
  onGraphDescriptionChange,
  onNodeFieldChange,
  onNodeValueChange,
}: NodeInspectorPanelProps) {
  const inspectorAccent = getInspectorAccent(node, edge);
  const selectionTabLabel = getInspectorSelectionTabLabel(node, edge);
  const edgeDetails = edge ? buildEdgeInspectorDetails(edge, nodes) : null;
  const selectionStateKey = edge ? `edge:${edge.id}` : node ? `node:${node.id}` : "graph";

  return (
    <Card className="editor-panel flex h-full min-h-[32rem] flex-col overflow-hidden">
      <CardHeader className="space-y-3 border-b border-white/6 pb-5">
        <div className="flex items-center justify-between gap-3">
          <div className="space-y-3">
            <p className="editor-kicker">Dock</p>
            <CardTitle className="display-font flex items-center gap-3 text-3xl tracking-[0.08em] text-white uppercase">
              <span className="editor-icon-shell">
                <Settings2 className="size-5 text-primary" />
              </span>
              Inspector
            </CardTitle>
          </div>

          <div
            className="size-3 rounded-full border border-white/20 shadow-[0_0_24px_rgba(255,157,28,0.24)]"
            style={{ backgroundColor: inspectorAccent }}
          />
        </div>

        <CardDescription className="leading-6 text-muted-foreground">
          Adjust graph metadata and inspect the currently selected node or link without leaving the canvas workflow.
        </CardDescription>
      </CardHeader>

      <CardContent className="min-h-0 flex-1 p-5 pt-5">
        <Tabs
          className="flex h-full flex-col"
          defaultValue={node || edge ? "selection" : "graph"}
          key={selectionStateKey}
        >
          <TabsList className="grid h-auto grid-cols-2 rounded-[1.2rem] border border-white/6 bg-black/25 p-1">
            <TabsTrigger
              className="rounded-[1rem] data-[state=active]:bg-white/10 data-[state=active]:text-white"
              value="graph"
            >
              Graph
            </TabsTrigger>
            <TabsTrigger
              className="rounded-[1rem] data-[state=active]:bg-white/10 data-[state=active]:text-white"
              value="selection"
            >
              {selectionTabLabel}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="graph" className="mt-4 flex-1">
            <div className="space-y-4">
              <div className="space-y-2">
                <Label className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]" htmlFor="graph-name">
                  Graph name
                </Label>
                <Input
                  className="h-12 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
                  id="graph-name"
                  value={graphName}
                  onChange={(event) => onGraphNameChange(event.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label
                  className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]"
                  htmlFor="graph-description"
                >
                  Graph description
                </Label>
                <Textarea
                  className="min-h-32 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
                  id="graph-description"
                  value={graphDescription}
                  onChange={(event) => onGraphDescriptionChange(event.target.value)}
                />
              </div>

              <div className="rounded-[1.25rem] border border-white/8 bg-black/20 p-4 text-sm leading-7 text-muted-foreground">
                Use the graph tab for session-level naming and summary text. Node and link details
                stay in the adjacent selection tab.
              </div>
            </div>
          </TabsContent>

          <TabsContent value="selection" className="mt-4 min-h-0 flex-1">
            {node ? (
              <ScrollArea className="h-full pr-2">
                <div className="space-y-4">
                  <div
                    className="rounded-[1.25rem] border border-white/8 bg-[linear-gradient(180deg,rgba(255,255,255,0.04),rgba(0,0,0,0.24))] p-4"
                    style={{ borderColor: inspectorAccent }}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="space-y-2">
                        <p className="display-font text-lg tracking-[0.06em] text-white uppercase">{node.data.label}</p>
                        <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">{node.data.nodeType}</p>
                      </div>
                      {template ? (
                        <Badge className="border-white/10 bg-primary/12 text-primary" variant="outline">
                          {template.category}
                        </Badge>
                      ) : null}
                    </div>

                    <p className="mt-3 text-sm leading-6 text-muted-foreground">
                      {node.data.description?.trim() || "This node is ready for field-level adjustments."}
                    </p>
                  </div>

                  <div className="space-y-2">
                    <Label className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]" htmlFor="node-label">
                      Label
                    </Label>
                    <Input
                      className="h-12 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
                      id="node-label"
                      value={node.data.label}
                      onChange={(event) => onNodeFieldChange("label", event.target.value)}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label
                      className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]"
                      htmlFor="node-description"
                    >
                      Description
                    </Label>
                    <Textarea
                      className="min-h-28 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
                      id="node-description"
                      value={node.data.description ?? ""}
                      onChange={(event) => onNodeFieldChange("description", event.target.value)}
                    />
                  </div>

                  {template?.fields?.length ? (
                    <>
                      <Separator className="bg-white/6" />
                      <div className="space-y-3">
                        <div className="flex items-center gap-2 text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                          <FilePenLine className="size-4 text-primary" />
                          Editable fields
                        </div>

                        {template.fields.map((field) => {
                          const currentValue = node.data.values?.[field.key];

                          if (field.kind === "boolean") {
                            return (
                              <label
                                key={field.key}
                                className="flex cursor-pointer items-center justify-between rounded-[1rem] border border-white/8 bg-black/25 px-4 py-3 text-sm text-[#edf3ff]"
                              >
                                <span className="font-medium">{field.label}</span>
                                <input
                                  checked={Boolean(currentValue)}
                                  className="size-4 rounded border-white/20 bg-transparent accent-[#ff9d1c]"
                                  onChange={(event) => onNodeValueChange(field.key, event.target.checked)}
                                  type="checkbox"
                                />
                              </label>
                            );
                          }

                          return (
                            <div className="space-y-2" key={field.key}>
                              <Label
                                className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]"
                                htmlFor={field.key}
                              >
                                {field.label}
                              </Label>
                              <Input
                                className="h-12 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
                                id={field.key}
                                placeholder={field.placeholder}
                                type={field.kind === "number" ? "number" : "text"}
                                value={String(currentValue ?? "")}
                                onChange={(event) =>
                                  onNodeValueChange(
                                    field.key,
                                    field.kind === "number"
                                      ? Number(event.target.value || 0)
                                      : event.target.value,
                                  )
                                }
                              />
                            </div>
                          );
                        })}
                      </div>
                    </>
                  ) : null}
                </div>
              </ScrollArea>
            ) : edgeDetails ? (
              <ScrollArea className="h-full pr-2">
                <div className="space-y-4">
                  <div
                    className="rounded-[1.25rem] border border-white/8 bg-[linear-gradient(180deg,rgba(255,255,255,0.04),rgba(0,0,0,0.24))] p-4"
                    style={{ borderColor: inspectorAccent }}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="space-y-2">
                        <p className="display-font text-lg tracking-[0.06em] text-white uppercase">{edgeDetails.title}</p>
                        <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">{edgeDetails.subtitle}</p>
                      </div>
                      <Badge className="border-white/10 bg-[#57c7ff]/12 text-[#8fdcff]" variant="outline">
                        Link
                      </Badge>
                    </div>

                    <p className="mt-3 text-sm leading-6 text-muted-foreground">{edgeDetails.description}</p>
                  </div>

                  <Separator className="bg-white/6" />

                  <div className="space-y-3">
                    <div className="flex items-center gap-2 text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                      <FilePenLine className="size-4 text-primary" />
                      Link details
                    </div>

                    {edgeDetails.items.map((item) => (
                      <div
                        className="rounded-[1rem] border border-white/8 bg-black/25 px-4 py-3"
                        key={item.label}
                      >
                        <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">{item.label}</p>
                        <p className="mt-2 text-sm text-[#edf3ff]">{item.value}</p>
                      </div>
                    ))}
                  </div>
                </div>
              </ScrollArea>
            ) : (
              <div className="rounded-[1.25rem] border border-dashed border-white/10 bg-black/20 p-5 text-sm leading-7 text-muted-foreground">
                Select a node or link on the canvas to inspect its details here.
              </div>
            )}
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}
