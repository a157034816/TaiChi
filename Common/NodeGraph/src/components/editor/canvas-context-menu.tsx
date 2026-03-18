"use client";

import { ClipboardPaste, Copy, Plus, Scissors, Trash2 } from "lucide-react";

import type { NodeLibraryItem } from "@/lib/nodegraph/types";

interface CanvasContextMenuProps {
  canCopy: boolean;
  canCut: boolean;
  canDelete: boolean;
  canPaste: boolean;
  copyLabel: string;
  cutLabel: string;
  deleteLabel: string;
  items: NodeLibraryItem[];
  position: {
    x: number;
    y: number;
  };
  onAddNode: (item: NodeLibraryItem) => void;
  onCopy: () => void;
  onCut: () => void;
  onDelete: () => void;
  onPaste: () => void;
}

function groupItemsByCategory(items: NodeLibraryItem[]) {
  const groups = new Map<string, NodeLibraryItem[]>();

  for (const item of items) {
    const currentGroup = groups.get(item.category) ?? [];
    currentGroup.push(item);
    groups.set(item.category, currentGroup);
  }

  return Array.from(groups.entries());
}

function CanvasMenuAction({
  disabled = false,
  icon: Icon,
  label,
  onClick,
}: {
  disabled?: boolean;
  icon: typeof Plus;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      className="canvas-context-menu__action"
      disabled={disabled}
      onClick={onClick}
      type="button"
    >
      <span className="canvas-context-menu__action-icon">
        <Icon className="size-4" />
      </span>
      <span>{label}</span>
    </button>
  );
}

export function CanvasContextMenu({
  canCopy,
  canCut,
  canDelete,
  canPaste,
  copyLabel,
  cutLabel,
  deleteLabel,
  items,
  position,
  onAddNode,
  onCopy,
  onCut,
  onDelete,
  onPaste,
}: CanvasContextMenuProps) {
  const itemGroups = groupItemsByCategory(items);

  return (
    <div
      className="canvas-context-menu nowheel nopan"
      data-canvas-context-menu="true"
      onContextMenu={(event) => event.preventDefault()}
      onMouseDown={(event) => event.stopPropagation()}
      style={{
        left: position.x,
        top: position.y,
      }}
    >
      <div className="canvas-context-menu__section">
        <p className="canvas-context-menu__label">Edit</p>
        <div className="canvas-context-menu__actions">
          <CanvasMenuAction disabled={!canCopy} icon={Copy} label={copyLabel} onClick={onCopy} />
          <CanvasMenuAction disabled={!canCut} icon={Scissors} label={cutLabel} onClick={onCut} />
          <CanvasMenuAction disabled={!canPaste} icon={ClipboardPaste} label="Paste nodes" onClick={onPaste} />
          <CanvasMenuAction disabled={!canDelete} icon={Trash2} label={deleteLabel} onClick={onDelete} />
        </div>
      </div>

      <div className="canvas-context-menu__divider" />

      <div className="canvas-context-menu__section">
        <p className="canvas-context-menu__label">Add node</p>
        <div className="canvas-context-menu__library">
          {itemGroups.map(([category, categoryItems]) => (
            <div className="canvas-context-menu__group" key={category}>
              <p className="canvas-context-menu__group-title">{category}</p>

              <div className="canvas-context-menu__actions">
                {categoryItems.map((item) => (
                  <button
                    className="canvas-context-menu__library-item"
                    key={item.type}
                    onClick={() => onAddNode(item)}
                    type="button"
                  >
                    <span className="canvas-context-menu__library-meta">
                      <span className="canvas-context-menu__action-icon">
                        <Plus className="size-4" />
                      </span>
                      <span>
                        <span className="canvas-context-menu__library-title">{item.label}</span>
                        <span className="canvas-context-menu__library-type">{item.type}</span>
                      </span>
                    </span>
                    <span className="canvas-context-menu__library-description">{item.description}</span>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
