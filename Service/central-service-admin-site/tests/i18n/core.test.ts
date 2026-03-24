import assert from "node:assert/strict";
import test from "node:test";

import {
  DEFAULT_LOCALE,
  compareText,
  formatDateTime,
  isSupportedLocale,
  resolveLocale,
  translate,
} from "../../src/i18n/core.ts";

/**
 * Verifies that unsupported locale values always fall back to the agreed
 * default instead of leaving the app in an inconsistent language state.
 */
test("resolveLocale falls back to zh-CN for unsupported inputs", () => {
  assert.equal(resolveLocale(undefined), DEFAULT_LOCALE);
  assert.equal(resolveLocale(""), DEFAULT_LOCALE);
  assert.equal(resolveLocale("fr-FR"), DEFAULT_LOCALE);
  assert.equal(resolveLocale("zh"), "zh-CN");
  assert.equal(resolveLocale("en"), "en-US");
});

/**
 * Verifies that locale detection only accepts the two supported languages.
 */
test("isSupportedLocale only accepts zh-CN and en-US", () => {
  assert.equal(isSupportedLocale("zh-CN"), true);
  assert.equal(isSupportedLocale("en-US"), true);
  assert.equal(isSupportedLocale("zh"), false);
  assert.equal(isSupportedLocale("en"), false);
});

/**
 * Verifies that message translation uses the active locale and supports
 * placeholder interpolation for user-facing copy.
 */
test("translate returns localized copy and interpolates placeholders", () => {
  assert.equal(translate("zh-CN", "common.refresh"), "刷新");
  assert.equal(translate("en-US", "common.refresh"), "Refresh");
  assert.equal(
    translate("en-US", "services.instancesLoadedButNotMatched", {
      loadedTotal: 3,
      serviceName: "Gateway",
    }),
    'Loaded 3 instances, but none matched service name "Gateway".'
  );
});

/**
 * Verifies that date formatting respects the selected locale instead of always
 * following the browser or machine defaults.
 */
test("formatDateTime formats using the requested locale", () => {
  const value = "2026-03-20T08:09:10.000Z";

  assert.equal(
    formatDateTime("zh-CN", value, { timeZone: "UTC" }),
    "2026/3/20 08:09:10"
  );
  assert.equal(
    formatDateTime("en-US", value, { timeZone: "UTC" }),
    "3/20/2026, 08:09:10"
  );
  assert.equal(formatDateTime("zh-CN", undefined), "-");
  assert.equal(formatDateTime("zh-CN", "not-a-date"), "not-a-date");
});

/**
 * Verifies that locale-aware comparison can be reused by list pages to keep
 * ordering stable across language switches.
 */
test("compareText uses locale-aware ordering", () => {
  const names = ["beta", "Alpha", "10", "2"];

  const sorted = [...names].sort((left, right) =>
    compareText("en-US", left, right)
  );

  assert.deepEqual(sorted, ["2", "10", "Alpha", "beta"]);
});
