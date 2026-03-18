"use client";

import { useDeferredValue } from "react";
import { Layers3, Plus, Search } from "lucide-react";

import type { NodeLibraryItem } from "@/lib/nodegraph/types";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";

interface NodeLibraryPanelProps {
  items: NodeLibraryItem[];
  searchTerm: string;
  onSearchTermChange: (value: string) => void;
  onAddNode: (item: NodeLibraryItem) => void;
}

export function NodeLibraryPanel({
  items,
  searchTerm,
  onSearchTermChange,
  onAddNode,
}: NodeLibraryPanelProps) {
  const deferredSearch = useDeferredValue(searchTerm);
  const keyword = deferredSearch.trim().toLowerCase();
  const filteredItems = !keyword
    ? items
    : items.filter((item) =>
        [item.label, item.description, item.category, item.type].some((value) =>
          value.toLowerCase().includes(keyword),
        ),
      );

  return (
    <Card className="editor-panel flex h-full min-h-[32rem] flex-col overflow-hidden">
      <CardHeader className="space-y-5 border-b border-white/6 pb-5">
        <div className="flex items-start justify-between gap-3">
          <div className="space-y-3">
            <p className="editor-kicker">Palette</p>
            <CardTitle className="display-font flex items-center gap-3 text-3xl tracking-[0.08em] text-white uppercase">
              <span className="editor-icon-shell">
                <Layers3 className="size-5 text-primary" />
              </span>
              Node library
            </CardTitle>
            <p className="text-sm leading-6 text-muted-foreground">
              Search domain nodes, compare categories, and drop building blocks straight onto the canvas.
            </p>
          </div>

          <Badge className="border-white/10 bg-black/30 text-[#d4deef]" variant="outline">
            {items.length} items
          </Badge>
        </div>

        <div className="relative">
          <Search className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="h-12 border-white/10 bg-black/35 pl-11 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
            value={searchTerm}
            onChange={(event) => onSearchTermChange(event.target.value)}
            placeholder="Search node types, categories, or labels"
          />
        </div>
      </CardHeader>

      <CardContent className="min-h-0 flex-1 p-4 pt-4">
        <ScrollArea className="h-full pr-2">
          <div className="space-y-3">
            {filteredItems.map((item) => (
              <button
                key={item.type}
                className="group w-full cursor-pointer rounded-[1.35rem] border border-white/8 bg-[linear-gradient(180deg,rgba(24,30,43,0.96),rgba(12,16,24,0.98))] p-4 text-left shadow-[0_16px_36px_rgba(0,0,0,0.22)] transition-all duration-200 hover:border-primary/40 hover:bg-[linear-gradient(180deg,rgba(30,38,55,0.98),rgba(12,16,24,0.98))] hover:shadow-[0_22px_48px_rgba(0,0,0,0.3)]"
                onClick={() => onAddNode(item)}
                type="button"
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="display-font text-lg tracking-[0.06em] text-white uppercase">{item.label}</p>
                      <span className="rounded-full border border-white/8 bg-white/5 px-2.5 py-1 text-[10px] uppercase tracking-[0.22em] text-[#8fa0bc]">
                        {item.type}
                      </span>
                    </div>
                    <p className="text-sm leading-6 text-muted-foreground">{item.description}</p>
                  </div>

                  <Badge className="border-white/10 bg-primary/12 text-primary" variant="outline">
                    {item.category}
                  </Badge>
                </div>

                <div className="mt-5 flex items-center justify-between gap-3 border-t border-white/6 pt-4 text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                  <span>{item.fields?.length ?? 0} editable fields</span>
                  <span className="inline-flex items-center gap-2 text-primary transition-transform duration-200 group-hover:translate-x-0.5">
                    <Plus className="size-3.5" />
                    Add node
                  </span>
                </div>
              </button>
            ))}

            {!filteredItems.length ? (
              <div className="rounded-[1.35rem] border border-dashed border-white/10 bg-black/20 p-5 text-sm leading-7 text-muted-foreground">
                No node types match the current search term.
              </div>
            ) : null}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
