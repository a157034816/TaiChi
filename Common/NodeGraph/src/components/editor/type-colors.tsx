"use client";

import { createContext, useContext, type ReactNode } from "react";

export type TypeColorMap = Map<string, string>;

const TypeColorsContext = createContext<TypeColorMap>(new Map());

export function TypeColorsProvider({
  value,
  children,
}: {
  value: TypeColorMap;
  children: ReactNode;
}) {
  return <TypeColorsContext.Provider value={value}>{children}</TypeColorsContext.Provider>;
}

export function useTypeColors() {
  return useContext(TypeColorsContext);
}
