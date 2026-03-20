import { describe, expect, it } from "vitest";

import {
  createLocalizedText,
  getEditorMessages,
  resolveFieldLabel,
  resolveFieldPlaceholder,
  resolveLocalizedText,
  resolveNodePortDefinitions,
} from "@/lib/nodegraph/localization";

describe("nodegraph localization helpers", () => {
  it("resolves localized text for the requested locale", () => {
    const value = createLocalizedText("开始", "Start");

    expect(resolveLocalizedText(value, "zh-CN")).toBe("开始");
    expect(resolveLocalizedText(value, "en")).toBe("Start");
  });

  it("localizes field and port metadata", () => {
    const portLabels = resolveNodePortDefinitions(
      [{ id: "next", label: createLocalizedText("下一步", "Next") }],
      "en",
    );
    const field = {
      key: "owner",
      label: createLocalizedText("负责人", "Owner"),
      placeholder: createLocalizedText("请输入负责人", "Enter owner"),
      kind: "text" as const,
    };

    expect(portLabels).toEqual([{ id: "next", label: "Next" }]);
    expect(resolveFieldLabel(field, "zh-CN")).toBe("负责人");
    expect(resolveFieldPlaceholder(field, "en")).toBe("Enter owner");
  });

  it("provides a Chinese-first editor message catalog", () => {
    const zhMessages = getEditorMessages("zh-CN");

    expect(zhMessages.header.activeGraphKicker).toBe("当前图谱");
    expect(zhMessages.graphDefaults.name("demo")).toBe("demo 流程图");
  });
});
