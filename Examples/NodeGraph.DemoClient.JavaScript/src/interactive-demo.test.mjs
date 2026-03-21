import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import test from "node:test";

test("interactive demo launcher uses a visual playground graph name by default", () => {
  const pythonCode = `
import importlib.util
import json
import pathlib
import sys

script_path = pathlib.Path("scripts/interactive-demo.py")
spec = importlib.util.spec_from_file_location("interactive_demo", script_path)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
sys.argv = ["interactive-demo.py"]
print(json.dumps(module.create_launch_config()))
`;

  const output = execFileSync("python", ["-X", "utf8", "-c", pythonCode], {
    cwd: process.cwd(),
    encoding: "utf8",
  });
  const payload = JSON.parse(output);

  assert.equal(payload.graph_name, "Interactive Visual Playground");
});
