function serializeForHtml(value) {
  return JSON.stringify(value).replace(/</g, "\\u003c");
}

export function renderHomePage({ config, state }) {
  const bootstrap = serializeForHtml({
    config,
    latestCompletion: state.latestCompletion,
    lastSession: state.lastSession,
  });

  return `<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>NodeGraph Demo Client</title>
    <style>
      :root {
        color-scheme: light;
        --bg: #f8f6ef;
        --ink: #17212b;
        --card: rgba(255, 255, 255, 0.8);
        --line: rgba(23, 33, 43, 0.12);
        --accent: #0f766e;
        --accent-2: #f59e0b;
      }

      * { box-sizing: border-box; }
      body {
        margin: 0;
        min-height: 100vh;
        font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
        color: var(--ink);
        background:
          radial-gradient(circle at top, rgba(15, 118, 110, 0.16), transparent 26%),
          radial-gradient(circle at bottom right, rgba(245, 158, 11, 0.22), transparent 22%),
          linear-gradient(135deg, #fffdf8, var(--bg));
      }

      main {
        width: min(1180px, calc(100% - 32px));
        margin: 0 auto;
        padding: 28px 0 40px;
      }

      .hero,
      .panel {
        background: var(--card);
        border: 1px solid var(--line);
        border-radius: 28px;
        backdrop-filter: blur(16px);
        box-shadow: 0 24px 80px rgba(15, 23, 42, 0.08);
      }

      .hero {
        padding: 32px;
        display: grid;
        gap: 24px;
      }

      .hero h1 {
        margin: 0;
        font-size: clamp(36px, 4vw, 58px);
        line-height: 1.04;
      }

      .hero p {
        margin: 0;
        max-width: 820px;
        font-size: 18px;
        line-height: 1.8;
        color: #5c6773;
      }

      .badges {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
      }

      .badge {
        display: inline-flex;
        padding: 8px 14px;
        border-radius: 999px;
        border: 1px solid var(--line);
        background: white;
        font-size: 12px;
        font-weight: 600;
        letter-spacing: 0.16em;
        text-transform: uppercase;
      }

      .grid {
        display: grid;
        gap: 18px;
        margin-top: 22px;
      }

      @media (min-width: 980px) {
        .grid {
          grid-template-columns: 1.3fr 0.9fr;
        }
      }

      .panel {
        padding: 24px;
      }

      .panel h2 {
        margin: 0 0 8px;
        font-size: 24px;
      }

      .panel p,
      .panel li,
      .panel label,
      .panel small {
        color: #5c6773;
        line-height: 1.7;
      }

      .stack {
        display: grid;
        gap: 14px;
      }

      input,
      select,
      textarea,
      button {
        font: inherit;
      }

      input,
      select {
        width: 100%;
        border-radius: 16px;
        border: 1px solid var(--line);
        background: rgba(255,255,255,0.82);
        padding: 12px 14px;
      }

      button,
      .link-button {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        text-decoration: none;
        border: none;
        border-radius: 999px;
        padding: 13px 20px;
        background: var(--accent);
        color: white;
        cursor: pointer;
        font-weight: 600;
      }

      button.secondary,
      .link-button.secondary {
        background: white;
        color: var(--ink);
        border: 1px solid var(--line);
      }

      .button-row {
        display: flex;
        flex-wrap: wrap;
        gap: 12px;
      }

      .link-button {
        display: none;
      }

      .link-button.is-visible {
        display: inline-flex;
      }

      .meta {
        display: grid;
        gap: 10px;
      }

      .meta strong {
        display: block;
        color: var(--ink);
      }

      pre {
        margin: 0;
        overflow: auto;
        border-radius: 18px;
        background: #132130;
        color: #d7f8f1;
        padding: 18px;
        min-height: 180px;
        line-height: 1.65;
      }

      .hint {
        padding: 14px 16px;
        border-radius: 18px;
        background: rgba(245, 158, 11, 0.12);
        color: #7c5609;
      }

      .session-card {
        display: grid;
        gap: 12px;
        padding: 18px;
        border-radius: 22px;
        background: rgba(255, 255, 255, 0.72);
        border: 1px solid var(--line);
      }

      .session-grid {
        display: grid;
        gap: 12px;
      }

      @media (min-width: 720px) {
        .session-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }

      .session-cell {
        display: grid;
        gap: 6px;
      }

      .session-cell strong {
        color: var(--ink);
      }

      .mono {
        font-family: "Cascadia Code", "SFMono-Regular", Consolas, monospace;
        word-break: break-all;
      }

      .status-note {
        padding: 14px 16px;
        border-radius: 18px;
        background: rgba(15, 118, 110, 0.1);
        color: #115e59;
      }
    </style>
  </head>
  <body>
    <main>
      <section class="hero">
        <div class="badges">
          <span class="badge">NodeGraph Demo Client</span>
          <span class="badge">Business-side Example</span>
          <span class="badge">JavaScript SDK</span>
        </div>
        <div class="stack">
          <h1>直接演示业务侧如何给 NodeGraph 提供节点库、接收完成回调，并创建编辑会话。</h1>
          <p>
            这个页面就是一个最小可运行的 demo client。你可以在这里发起新图编辑或编辑已有示例图，
            然后打开 NodeGraph 返回的编辑页。完成提交后，页面会自动显示最新回调结果。
          </p>
        </div>
      </section>

      <section class="grid">
        <div class="panel stack">
          <div>
            <h2>1. 创建编辑会话</h2>
            <p>选择模式、填一个图名称，然后让 demo client 通过 JavaScript SDK 去请求 NodeGraph。</p>
          </div>
          <div class="stack">
            <label>
              节点图模式
              <select id="graphMode">
                <option value="new">新建节点图</option>
                <option value="existing">编辑已有示例图</option>
              </select>
            </label>
            <label>
              节点图名称
              <input id="graphName" value="Demo Approval Flow" />
            </label>
            <div class="button-row">
              <button id="createSessionButton" type="button">Create editor session</button>
              <button id="refreshButton" class="secondary" type="button">Refresh latest callback</button>
            </div>
          </div>
          <div class="meta">
            <div>
              <strong>NodeGraph base URL</strong>
              <span id="nodeGraphBaseUrl"></span>
            </div>
            <div>
              <strong>Demo client base URL</strong>
              <span id="demoClientBaseUrl"></span>
            </div>
            <div>
              <strong>Current domain</strong>
              <span id="demoDomain"></span>
            </div>
          </div>
          <div class="hint">
            如果创建成功，你会看到 <code>editorUrl</code>。打开它后，在 NodeGraph 编辑器里点击 “Complete editing”，
            demo client 就会收到完成回调。
          </div>
        </div>

        <div class="panel stack">
          <div>
            <h2>2. 最新会话</h2>
            <p>最近一次通过 demo client 创建出来的编辑会话。你可以直接从这里拿到并打开 <code>editorUrl</code>。</p>
          </div>
          <div class="session-card">
            <div class="session-grid">
              <div class="session-cell">
                <strong>Session ID</strong>
                <span id="sessionId" class="mono">Not created yet.</span>
              </div>
              <div class="session-cell">
                <strong>Access type</strong>
                <span id="accessType">-</span>
              </div>
              <div class="session-cell">
                <strong>Domain cache</strong>
                <span id="domainCached">-</span>
              </div>
              <div class="session-cell">
                <strong>Created at</strong>
                <span id="createdAt">-</span>
              </div>
              <div class="session-cell" style="grid-column: 1 / -1;">
                <strong>Editor URL</strong>
                <span id="editorUrl" class="mono">Create a session to receive an editor URL.</span>
              </div>
            </div>
            <div class="button-row">
              <a id="editorLink" class="link-button secondary" href="/" target="_blank" rel="noreferrer">Open editor page</a>
            </div>
            <div id="sessionHint" class="status-note">
              你也可以使用一键联调脚本自动创建 session。拿到 <code>editorUrl</code> 之后，手动打开编辑页试玩并点击保存，再回到这里查看最新回调。
            </div>
          </div>
          <pre id="sessionOutput">No session has been created yet.</pre>
        </div>
      </section>

      <section class="grid">
        <div class="panel stack">
          <div>
            <h2>3. 最新完成回调</h2>
            <p>NodeGraph 调用 <code>/api/completed</code> 后，最新节点图结果会显示在这里。</p>
          </div>
          <pre id="completionOutput">No completion payload received yet.</pre>
        </div>

        <div class="panel stack">
          <div>
            <h2>4. 当前节点库</h2>
            <p>NodeGraph 首次见到当前 domain 时，会从本服务的 <code>/api/node-library</code> 拉取这些节点。</p>
          </div>
          <pre id="libraryOutput"></pre>
        </div>
      </section>
    </main>

    <script>
      const bootstrap = ${bootstrap};
      const graphModeInput = document.getElementById("graphMode");
      const graphNameInput = document.getElementById("graphName");
      const sessionOutput = document.getElementById("sessionOutput");
      const completionOutput = document.getElementById("completionOutput");
      const libraryOutput = document.getElementById("libraryOutput");
      const nodeGraphBaseUrl = document.getElementById("nodeGraphBaseUrl");
      const demoClientBaseUrl = document.getElementById("demoClientBaseUrl");
      const demoDomain = document.getElementById("demoDomain");
      const sessionId = document.getElementById("sessionId");
      const accessType = document.getElementById("accessType");
      const domainCached = document.getElementById("domainCached");
      const createdAt = document.getElementById("createdAt");
      const editorUrl = document.getElementById("editorUrl");
      const editorLink = document.getElementById("editorLink");
      const sessionHint = document.getElementById("sessionHint");

      function pretty(value, fallback) {
        return value ? JSON.stringify(value, null, 2) : fallback;
      }

      function applySession(lastSession) {
        const response = lastSession?.response ?? null;
        const latestEditorUrl = response?.editorUrl ?? "";

        sessionOutput.textContent = pretty(lastSession, "No session has been created yet.");
        sessionId.textContent = response?.sessionId ?? "Not created yet.";
        accessType.textContent = response?.accessType ?? "-";
        domainCached.textContent = typeof response?.domainCached === "boolean" ? String(response.domainCached) : "-";
        createdAt.textContent = lastSession?.createdAt ?? "-";
        editorUrl.textContent = latestEditorUrl || "Create a session to receive an editor URL.";

        if (latestEditorUrl) {
          editorLink.href = latestEditorUrl;
          editorLink.classList.add("is-visible");
          sessionHint.textContent =
            "已经拿到 editorUrl。现在请手动打开编辑页，拖动或修改节点后点击 “Complete editing”，然后回到这个页面查看最新回调。";
          return;
        }

        editorLink.classList.remove("is-visible");
        sessionHint.textContent =
          "你也可以使用一键联调脚本自动创建 session。拿到 editorUrl 之后，手动打开编辑页试玩并点击保存，再回到这里查看最新回调。";
      }

      async function loadLatest() {
        const response = await fetch("/api/results/latest");
        const payload = await response.json();
        applySession(payload.lastSession);
        completionOutput.textContent = pretty(payload.latestCompletion, "No completion payload received yet.");
      }

      async function loadLibrary() {
        const response = await fetch("/api/node-library");
        const payload = await response.json();
        libraryOutput.textContent = JSON.stringify(payload, null, 2);
      }

      async function createSession() {
        sessionOutput.textContent = "Creating session...";
        const response = await fetch("/api/create-session", {
          method: "POST",
          headers: {
            "content-type": "application/json"
          },
          body: JSON.stringify({
            graphMode: graphModeInput.value,
            graphName: graphNameInput.value
          })
        });

        const payload = await response.json();
        if (!response.ok) {
          sessionOutput.textContent = JSON.stringify(payload, null, 2);
          return;
        }

        await loadLatest();
      }

      nodeGraphBaseUrl.textContent = bootstrap.config.nodeGraphBaseUrl;
      demoClientBaseUrl.textContent = bootstrap.config.demoClientBaseUrl;
      demoDomain.textContent = bootstrap.config.demoDomain;
      applySession(bootstrap.lastSession);
      completionOutput.textContent = pretty(bootstrap.latestCompletion, "No completion payload received yet.");

      document.getElementById("createSessionButton").addEventListener("click", createSession);
      document.getElementById("refreshButton").addEventListener("click", loadLatest);

      loadLibrary();
      setInterval(loadLatest, 3000);
    </script>
  </body>
</html>`;
}
