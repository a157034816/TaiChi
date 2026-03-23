import { describe, expect, it } from "vitest";

import {
  createI18nRuntime,
  DEFAULT_LOCALE,
  getAvailableLocaleCodes,
  getBuiltinLocaleCodes,
  resolveFieldLabel,
  resolveFieldPlaceholder,
  resolveNodeCategory,
  resolveNodeDescription,
  resolveNodeLabel,
  resolvePortLabel,
} from "@/lib/nodegraph/localization";

const domainI18n = {
  defaultLocale: "en",
  locales: {
    en: {
      "categories.control": "Control",
      "fields.owner.label": "Owner",
      "fields.owner.placeholder": "Enter owner",
      "nodes.start.description": "Entry point for a new workflow.",
      "nodes.start.label": "Start",
      "ports.next": "Next",
    },
    "zh-CN": {
      "categories.control": "控制",
      "fields.owner.label": "负责人",
      "fields.owner.placeholder": "请输入负责人",
      "nodes.start.description": "新工作流的入口节点。",
      "nodes.start.label": "开始",
      "ports.next": "下一步",
    },
  },
} as const;

const domainI18nWithFrench = {
  defaultLocale: "en",
  locales: {
    ...domainI18n.locales,
    "fr-FR": {
      "categories.control": "Controle",
      "ports.next": "Suivant",
    },
  },
} as const;

describe("nodegraph localization helpers", () => {
  it("reads bundled editor copy from JSON catalogs", () => {
    const i18n = createI18nRuntime({
      locale: DEFAULT_LOCALE,
    });

    expect(getBuiltinLocaleCodes()).toEqual(expect.arrayContaining(["en", "zh-CN"]));
    expect(i18n.text("editor.header.activeGraphKicker")).toBe("当前图谱");
    expect(i18n.text("editor.graphDefaults.name", { domain: "demo" })).toBe("demo 流程图");
  });

  it("exposes builtin and domain locales through the runtime", () => {
    const i18n = createI18nRuntime({
      locale: "fr-FR",
      domainI18n: domainI18nWithFrench,
    });

    expect(getAvailableLocaleCodes(domainI18nWithFrench)).toEqual(["zh-CN", "en", "fr-FR"]);
    expect(i18n.availableLocales).toEqual(["zh-CN", "en", "fr-FR"]);
    expect(i18n.getLocaleLabel("fr-FR")).toBeTruthy();
  });

  it("returns raw node-library metadata without applying locale translation", () => {
    const i18n = createI18nRuntime({
      locale: "zh-CN",
      domainI18n,
    });

    const port = {
      id: "next",
      label: "Next",
    };
    const field = {
      key: "owner",
      kind: "text" as const,
      label: "Owner",
      placeholder: "Enter owner",
    };

    expect(resolvePortLabel(port, i18n)).toBe("Next");
    expect(resolveFieldLabel(field, i18n)).toBe("Owner");
    expect(resolveFieldPlaceholder(field, i18n)).toBe("Enter owner");
  });

  it("keeps raw labels stable even when the active locale is missing", () => {
    const i18n = createI18nRuntime({
      locale: "fr-FR",
      domainI18n,
    });

    expect(resolvePortLabel({ id: "next", label: "Next" }, i18n)).toBe("Next");
  });

  it("prefers node overrides and otherwise uses the stored raw strings", () => {
    const i18n = createI18nRuntime({
      locale: "en",
      domainI18n,
    });

    expect(
      resolveNodeLabel(
        {
          label: "Start",
        },
        i18n,
      ),
    ).toBe("Start");
    expect(
      resolveNodeDescription(
        {
          description: "Entry point for a new workflow.",
        },
        i18n,
      ),
    ).toBe("Entry point for a new workflow.");
    expect(
      resolveNodeLabel(
        {
          label: "Start",
          labelOverride: "Manual Start",
        },
        i18n,
      ),
    ).toBe("Manual Start");
    expect(
      resolveNodeDescription(
        {
          description: "Legacy snapshot",
        },
        i18n,
      ),
    ).toBe("Legacy snapshot");
    expect(
      resolveNodeCategory(
        {
          category: "Control",
        },
        i18n,
      ),
    ).toBe("Control");
  });
});
