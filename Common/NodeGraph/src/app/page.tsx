import Link from "next/link";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";

export default function Home() {
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-6 py-10 lg:px-10">
      <section className="panel-surface relative overflow-hidden rounded-[2rem] border border-white/60 px-6 py-8 shadow-[0_24px_80px_rgba(15,23,42,0.12)] lg:px-10 lg:py-12">
        <div className="absolute inset-y-0 right-0 hidden w-1/3 bg-[radial-gradient(circle_at_center,rgba(15,118,110,0.18),transparent_68%)] lg:block" />
        <div className="relative grid gap-10 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
          <div className="space-y-6">
            <div className="flex flex-wrap gap-3">
              <Badge variant="secondary">NodeGraph</Badge>
              <Badge variant="outline">Next.js + React Flow + shadcn</Badge>
              <Badge variant="outline">SDK-first workflow editor</Badge>
            </div>
            <div className="space-y-4">
              <p className="display-font text-sm uppercase tracking-[0.32em] text-muted-foreground">
                Domain-aware visual flow editing
              </p>
              <h1 className="display-font max-w-3xl text-4xl leading-tight font-semibold text-balance sm:text-5xl lg:text-6xl">
                Build a node-graph editing URL for every client request, then hand the result back
                with one webhook.
              </h1>
              <p className="max-w-3xl text-lg leading-8 text-muted-foreground">
                NodeGraph is designed for SDK consumers: you create an editing session, we resolve
                the domain node library on first contact, return the correct editor URL for the
                caller network, and post the final graph back to your business system when the user
                finishes editing.
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
              <CardDescription>What happens when a client asks to edit a graph.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4 text-sm leading-6 text-muted-foreground">
              <p>1. Client sends `domain`, node-library endpoint, completion webhook, and graph.</p>
              <p>2. NodeGraph fetches and caches the domain node library on first encounter.</p>
              <p>3. NodeGraph returns a public or private editor URL based on the caller IP.</p>
              <p>4. User edits the graph in the browser and submits the final version.</p>
              <p>5. NodeGraph calls the client webhook with the finished graph document.</p>
            </CardContent>
          </Card>
        </div>
      </section>

      <section className="grid gap-6 py-10 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-2xl">Per-domain caching</CardTitle>
            <CardDescription>Each client owns its domain identity.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-7 text-muted-foreground">
            Node libraries are cached in memory by domain, so subsequent editing sessions can be
            created quickly while keeping business-specific node catalogs isolated.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-2xl">Network-aware URL</CardTitle>
            <CardDescription>One API call, the right base URL.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-7 text-muted-foreground">
            NodeGraph inspects the request IP and chooses an internal or public base URL before it
            returns the editor entry link to the SDK caller.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-2xl">Webhook completion</CardTitle>
            <CardDescription>Business systems stay in control of persistence.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-7 text-muted-foreground">
            Final graph data is posted back to the client webhook. Your application decides how to
            store it, validate it, or continue downstream business processing.
          </CardContent>
        </Card>
      </section>

      <section id="quickstart" className="grid gap-6 py-4 lg:grid-cols-[minmax(0,1fr)_24rem]">
        <Card>
          <CardHeader>
            <CardTitle className="display-font text-3xl">Quick contract</CardTitle>
            <CardDescription>The minimum payload needed to request an editing session.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <pre className="overflow-x-auto rounded-[1.5rem] bg-[#152230] p-5 text-sm leading-7 text-[#d6f6ef]">
              {`POST /api/sdk/sessions
{
  "domain": "erp-workflow",
  "clientName": "TaiChi ERP",
  "nodeLibraryEndpoint": "https://client.example.com/nodegraph/library",
  "completionWebhook": "https://client.example.com/nodegraph/completed",
  "graph": {
    "name": "审批流程",
    "nodes": [],
    "edges": [],
    "viewport": { "x": 0, "y": 0, "zoom": 1 }
  },
  "metadata": {
    "ticketId": "WF-1001"
  }
}`}
            </pre>
            <Separator />
            <div className="space-y-2 text-sm leading-7 text-muted-foreground">
              <p>
                The client node-library endpoint can return either an array of node definitions or
                an object shaped like <code>{`{ "nodes": [...] }`}</code>.
              </p>
              <p>
                Each node definition may optionally declare <code>inputs</code> and{" "}
                <code>outputs</code> for Blueprint-style multi-port nodes. If they are omitted,
                NodeGraph falls back to one input and one output.
              </p>
              <p>
                Port definitions can also include an optional <code>dataType</code> so the editor
                can suggest compatible nodes when a user drops a connection onto empty canvas space.
                Use programming-language type names such as <code>string</code>, <code>boolean</code>,
                or <code>WorkflowRequest</code> rather than business status labels.
              </p>
              <p>
                Persisted edges may include <code>sourceHandle</code> and <code>targetHandle</code>{" "}
                so the editor can restore which specific port each link used.
              </p>
              <p>
                Set <code>NODEGRAPH_PUBLIC_BASE_URL</code> and{" "}
                <code>NODEGRAPH_PRIVATE_BASE_URL</code> to control the editor URL returned to the
                SDK caller.
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
              <p>Node-library fetch and completion webhook both default to 5000ms.</p>
            </div>
            <Separator />
            <p>
              Use the SDK packages under <code>SDK/NodeGraph</code> to build these requests from
              JavaScript, .NET, or Rust.
            </p>
            <p>
              SDK graph edge models also preserve <code>sourceHandle</code> and{" "}
              <code>targetHandle</code> for multi-port graphs.
            </p>
          </CardContent>
        </Card>
      </section>
    </main>
  );
}
