import type { Metadata } from "next";
import {
  Chakra_Petch,
  IBM_Plex_Sans,
  Share_Tech_Mono,
  Space_Grotesk,
} from "next/font/google";
import "@xyflow/react/dist/style.css";
import "./globals.css";

const bodyFont = IBM_Plex_Sans({
  variable: "--font-body",
  subsets: ["latin"],
});

const headingFont = Space_Grotesk({
  variable: "--font-display",
  subsets: ["latin"],
});

const editorBodyFont = Chakra_Petch({
  variable: "--font-editor-body",
  subsets: ["latin"],
  weight: ["300", "400", "500", "600", "700"],
});

const editorDisplayFont = Share_Tech_Mono({
  variable: "--font-editor-display",
  subsets: ["latin"],
  weight: "400",
});

export const metadata: Metadata = {
  title: "NodeGraph",
  description: "A full-stack node graph editing service with SDK-friendly APIs.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <body
        className={`${bodyFont.variable} ${headingFont.variable} ${editorBodyFont.variable} ${editorDisplayFont.variable} antialiased`}
      >
        <div className="app-shell">{children}</div>
      </body>
    </html>
  );
}
