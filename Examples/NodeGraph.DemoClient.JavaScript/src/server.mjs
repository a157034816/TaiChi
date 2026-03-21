import { createServer } from "node:http";

import { createApp } from "./app.mjs";
import { getDemoConfig } from "./config.mjs";

const config = getDemoConfig();
const server = createServer(createApp({ config }));

server.listen(config.port, config.host, () => {
  console.log(`[NodeGraph Demo Client] listening on http://${config.host}:${config.port}`);
  console.log(`[NodeGraph Demo Client] browser page: ${config.demoClientBaseUrl}/`);
  console.log(`[NodeGraph Demo Client] node library: ${config.demoClientBaseUrl}/api/node-library`);
  console.log(`[NodeGraph Demo Client] completion webhook: ${config.demoClientBaseUrl}/api/completed`);
  console.log(`[NodeGraph Demo Client] target NodeGraph: ${config.nodeGraphBaseUrl}`);
});
