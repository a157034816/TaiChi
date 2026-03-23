import Link from "next/link";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";

const registerRuntimeExample = `POST /api/sdk/runtimes/register
{
  "runtimeId": "rt_hello_world_001",
  "domain": "hello-world",
  "clientName": "TaiChi Hello World Host",
  "controlBaseUrl": "https://client.example.com/nodegraph/runtime",
  "libraryVersion": "hello-world@1",
  "capabilities": {
    "canExecute": true,
    "canDebug": true,
    "canProfile": true
  },
  "library": {
    "nodes": [
      {
        "type": "greeting_source",
        "displayName": "Greeting Source",
        "description": "Create the greeting text.",
        "category": "Hello World",
        "outputs": [
          { "id": "text", "label": "Text", "dataType": "hello/text" }
        ],
        "fields": [
          {
            "key": "name",
            "label": "Name",
            "placeholder": "Who should be greeted?",
            "kind": "text",
            "defaultValue": "World"
          }
        ]
      }
    ],
    "typeMappings": [
      { "canonicalId": "hello/text", "type": "String", "color": "#2563eb" }
    ]
  }
}`;

const createSessionExample = `POST /api/sdk/sessions
{
  "runtimeId": "rt_hello_world_001",
  "completionWebhook": "https://client.example.com/nodegraph/completed",
  "graph": {
    "name": "Hello World Pipeline",
    "nodes": [],
    "edges": [],
    "viewport": { "x": 0, "y": 0, "zoom": 1 }
  },
  "metadata": {
    "ticketId": "HW-1001"
  }
}`;

const refreshExample = `POST /api/editor/sessions/{sessionId}/library/refresh
{
  "graph": {
    "name": "Hello World Pipeline",
    "nodes": [],
    "edges": [],
    "viewport": { "x": 0, "y": 0, "zoom": 1 }
  }
}`;

export default function Home() {
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-6 py-10 lg:px-10">
      <section className="panel-surface relative overflow-hidden rounded-[2rem] border border-white/60 px-6 py-8 shadow-[0_24px_80px_rgba(15,23,42,0.12)] lg:px-10 lg:py-12">
        <div className="absolute inset-y-0 right-0 hidden w-1/3 bg-[radial-gradient(circle_at_center,rgba(15,118,110,0.18),transparent_68%)] lg:block" />
        <div className="relative grid gap-10 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
          <div className="space-y-6">
            <div className="flex flex-wrap gap-3">
              <Badge variant="secondary">NodeGraph</Badge>
              <Badge variant="outline">Runtime registry</Badge>
              <Badge variant="outline">SDK-hosted execution</Badge>
            </div>
            <div className="space-y-4">
              <p className="display-font text-sm uppercase tracking-[0.32em] text-muted-foreground">
                Runtime-aware visual flow editing
              </p>
              <h1 className="display-font max-w-3xl text-4xl leading-tight font-semibold text-balance sm:text-5xl lg:text-6xl">
                Register the runtime once, open editor sessions by <code>runtimeId</code>, and
                keep execution inside your SDK host.
              </h1>
              <p className="max-w-3xl text-lg leading-8 text-muted-foreground">
                NodeGraph no longer pulls a node library from a remote endpoint during session
                creation. Your host initializes a runtime in memory, submits the node-library
                snapshot up front, optionally refreshes it later, and receives the finished graph
                back through one completion webhook.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <Button asChild size="lg">
                <Link href="#quickstart">Read the contract</Link>
              </Button>
              <Button asChild size="lg" variant="outline">
                <Link href="/api/health">Health endpoint</Link>
              </Button>
            </div>
          </div>
          <Card className="border-white/60">
            <CardHeader>
              <CardTitle className="display-font text-2xl">Session flow</CardTitle>
              <CardDescription>What happens when a host wants to edit a graph.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4 text-sm leading-6 text-muted-foreground">
              <p>1. Host creates a runtime, generates a unique `runtimeId`, and exports a library snapshot.</p>
              <p>2. Host calls `POST /api/sdk/runtimes/register` with library, version, and capabilities.</p>
              <p>3. Host creates an editor session with `runtimeId + completionWebhook + graph?`.</p>
              <p>4. The editor may force-refresh the runtime library from `GET controlBaseUrl/library`.</p>
              <p>5. NodeGraph posts the final graph back to the host webhook when editing completes.</p>
            </CardContent>
          </Card>
        </div>
      </section>

      <section className="grid gap-6 py-10 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-2xl">Runtime cache</CardTitle>
            <CardDescription>Cache is keyed by `runtimeId` for 30 minutes.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-7 text-muted-foreground">
            The SDK can skip redundant registrations locally, while NodeGraph keeps the registered
            runtime available for repeated session creation until the cache expires or the editor
            requests a forced refresh.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-2xl">Raw library text</CardTitle>
            <CardDescription>Node libraries are no longer translated by NodeGraph.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-7 text-muted-foreground">
            Display names, descriptions, categories, field labels, and placeholders are rendered
            exactly as the host provides them. NodeGraph i18n now applies only to the editor UI
            chrome.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-2xl">Host-owned runtime</CardTitle>
            <CardDescription>Execution, debugging, and profiling stay in your process.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-7 text-muted-foreground">
            The SDK runtimes execute graphs, produce profiler snapshots, and support breakpoints.
            NodeGraph focuses on editing, runtime cache management, and completion webhooks.
          </CardContent>
        </Card>
      </section>

      <section id="quickstart" className="grid gap-6 py-4 lg:grid-cols-[minmax(0,1fr)_24rem]">
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-3xl">Quick contract</CardTitle>
            <CardDescription>The minimum runtime-first workflow for NodeGraph.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="space-y-3">
              <p className="text-sm font-medium text-foreground">1. Register the runtime and node library</p>
              <pre className="overflow-x-auto rounded-[1.5rem] bg-[#152230] p-5 text-sm leading-7 text-[#d6f6ef]">
                {registerRuntimeExample}
              </pre>
            </div>
            <div className="space-y-3">
              <p className="text-sm font-medium text-foreground">2. Create a session using only `runtimeId`</p>
              <pre className="overflow-x-auto rounded-[1.5rem] bg-[#152230] p-5 text-sm leading-7 text-[#d6f6ef]">
                {createSessionExample}
              </pre>
            </div>
            <div className="space-y-3">
              <p className="text-sm font-medium text-foreground">3. Force-refresh the cached library when needed</p>
              <pre className="overflow-x-auto rounded-[1.5rem] bg-[#152230] p-5 text-sm leading-7 text-[#d6f6ef]">
                {refreshExample}
              </pre>
            </div>
            <Separator />
            <div className="space-y-2 text-sm leading-7 text-muted-foreground">
              <p>
                NodeGraph caches runtimes by <code>runtimeId</code>; it does not fetch a{" "}
                <code>nodeLibraryEndpoint</code> during <code>createSession</code> anymore.
              </p>
              <p>
                Re-registering the same runtime returns <code>cached: true</code> when the version,
                domain, and <code>controlBaseUrl</code> match; SDK hosts normally skip this call
                locally until the 30-minute TTL expires.
              </p>
              <p>
                The refresh API reads <code>GET {"{controlBaseUrl}"}/library</code>, updates the
                runtime cache, and immediately returns the latest node library plus a migrated graph
                payload for the current editor session.
              </p>
              <p>
                Node-library fields use raw strings such as <code>displayName</code>,{" "}
                <code>description</code>, <code>category</code>, port <code>label</code>, and
                field <code>label</code>/<code>placeholder</code>. Old key-based fields are
                compatibility-only.
              </p>
              <p>
                Port <code>dataType</code> values should use cross-language canonical ids such as{" "}
                <code>hello/text</code>. Optional <code>typeMappings</code> map canonical ids back
                to runtime-specific types and may also provide a display color.
              </p>
              <p>
                <code>select</code> fields still use{" "}
                <code>GET /api/editor/sessions/{"{sessionId}"}/field-options</code>, and persisted
                edges may keep <code>sourceHandle</code>/<code>targetHandle</code> for multi-port
                graphs.
              </p>
              <p>
                Configure <code>NODEGRAPH_PUBLIC_BASE_URL</code>,{" "}
                <code>NODEGRAPH_PRIVATE_BASE_URL</code>, and{" "}
                <code>NODEGRAPH_RUNTIME_CACHE_TTL_MS</code> to control editor URLs and cache
                lifetime.
              </p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="display-font text-3xl">Project defaults</CardTitle>
            <CardDescription>Environment values used by this service.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm leading-7 text-muted-foreground">
            <div>
              <p className="font-medium text-foreground">Public URL</p>
              <p>Falls back to `http://localhost:3000`.</p>
            </div>
            <div>
              <p className="font-medium text-foreground">Private URL</p>
              <p>Falls back to `http://127.0.0.1:3000`.</p>
            </div>
            <div>
              <p className="font-medium text-foreground">Timeouts</p>
              <p>Runtime library refresh and completion webhook both default to `5000ms`.</p>
            </div>
            <div>
              <p className="font-medium text-foreground">Runtime cache TTL</p>
              <p>Defaults to `1800000ms` (`30` minutes).</p>
            </div>
            <Separator />
            <p>
              Use the SDK packages under <code>SDK/NodeGraph</code> to register runtimes and build
              sessions from JavaScript, .NET, or Rust.
            </p>
            <p>
              For end-to-end samples, check the Hello World hosts under{" "}
              <code>Examples/NodeGraph.DemoClient.JavaScript</code>,{" "}
              <code>Examples/NodeGraph.DemoClient.DotNet</code>, and{" "}
              <code>Examples/NodeGraph.DemoClient.Rust</code>.
            </p>
          </CardContent>
        </Card>
      </section>
    </main>
  );
}
