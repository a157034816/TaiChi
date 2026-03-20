"use client";

import { forwardRef } from "react";
import { createPortal } from "react-dom";
import { ClipboardPaste, Copy, Plus, Scissors, Trash2 } from "lucide-react";

import { useEditorI18n } from "@/components/editor/editor-i18n-context";
import type { NodeLibraryItem } from "@/lib/nodegraph/types";
import {
  resolveNodeLibraryCategory,
  resolveNodeLibraryDescription,
  resolveNodeLibraryLabel,
} from "@/lib/nodegraph/localization";

interface CanvasContextMenuProps {
  canCopy: boolean;
  canCut: boolean;
  canDelete: boolean;
  canPaste: boolean;
  copyLabel: string;
  cutLabel: string;
  deleteLabel: string;
  emptyStateMessage: string;
  items: NodeLibraryItem[];
  isConnectionCreation: boolean;
  libraryLabel: string;
  position: {
    x: number;
    y: number;
  };
  showLibrary: boolean;
  onAddNode: (item: NodeLibraryItem) => void;
  onCopy: () => void;
  onCut: () => void;
  onDelete: () => void;
  onPaste: () => void;
}

function groupItemsByCategory(items: NodeLibraryItem[], i18n: ReturnType<typeof useEditorI18n>) {
  const groups = new Map<string, NodeLibraryItem[]>();

  for (const item of items) {
    const category = resolveNodeLibraryCategory(item, i18n);
    const currentGroup = groups.get(category) ?? [];
    currentGroup.push(item);
    groups.set(category, currentGroup);
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

/**
 * Renders the right-click context menu for editing and node insertion actions.
 */
export const CanvasContextMenu = forwardRef<HTMLDivElement, CanvasContextMenuProps>(function CanvasContextMenu(
  {
    canCopy,
    canCut,
    canDelete,
    canPaste,
    copyLabel,
    cutLabel,
    deleteLabel,
    emptyStateMessage,
    items,
    isConnectionCreation,
    libraryLabel,
    position,
    showLibrary,
    onAddNode,
    onCopy,
    onCut,
    onDelete,
    onPaste,
  },
  ref,
) {
  const i18n = useEditorI18n();
  const itemGroups = groupItemsByCategory(items, i18n);
  const editActions = [
    canCopy
      ? { key: "copy", icon: Copy, label: copyLabel, onClick: onCopy }
      : null,
    canCut
      ? { key: "cut", icon: Scissors, label: cutLabel, onClick: onCut }
      : null,
    canPaste
      ? { key: "paste", icon: ClipboardPaste, label: i18n.text("editor.contextMenu.pasteNodes"), onClick: onPaste }
      : null,
    canDelete
      ? { key: "delete", icon: Trash2, label: deleteLabel, onClick: onDelete }
      : null,
  ].filter((action) => action !== null);
  const content = (
    <div
      ref={ref}
      className="canvas-context-menu nowheel nopan"
      data-canvas-context-menu="true"
      onContextMenu={(event) => event.preventDefault()}
      onMouseDown={(event) => event.stopPropagation()}
      style={{
        left: position.x,
        top: position.y,
      }}
    >
      {!isConnectionCreation && editActions.length ? (
        <>
          <div className="canvas-context-menu__section">
            <p className="canvas-context-menu__label">{i18n.text("editor.contextMenu.edit")}</p>
            <div className="canvas-context-menu__actions">
              {editActions.map((action) => (
                <CanvasMenuAction
                  icon={action.icon}
                  key={action.key}
                  label={action.label}
                  onClick={action.onClick}
                />
              ))}
            </div>
          </div>

          <div className="canvas-context-menu__divider" />
        </>
      ) : null}

      {showLibrary ? (
        <div className="canvas-context-menu__section canvas-context-menu__section--library">
          <p className="canvas-context-menu__label">{libraryLabel}</p>
          <div className="canvas-context-menu__library">
            {itemGroups.length ? (
              itemGroups.map(([category, categoryItems]) => (
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
                            <span className="canvas-context-menu__library-title">
                              {resolveNodeLibraryLabel(item, i18n)}
                            </span>
                            <span className="canvas-context-menu__library-type">{item.type}</span>
                          </span>
                        </span>
                        <span className="canvas-context-menu__library-description">
                          {resolveNodeLibraryDescription(item, i18n)}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>
              ))
            ) : (
              <p className="canvas-context-menu__library-description">{emptyStateMessage}</p>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );

  if (typeof document === "undefined") {
    return content;
  }

  return createPortal(content, document.body);
});
