import { QueryClient } from "@tanstack/react-query";
import { createHashHistory, createRouter } from "@tanstack/react-router";
import { routeTree } from "./routeTree.gen";

const shouldUseHashHistory = () =>
  import.meta.env.MODE === "phone" ||
  (typeof window !== "undefined" &&
    (window.location.protocol === "file:" || Boolean(window.mauiWebber)));

export const getRouter = () => {
  const queryClient = new QueryClient();

  const router = createRouter({
    routeTree,
    context: { queryClient },
    history: shouldUseHashHistory() ? createHashHistory() : undefined,
    scrollRestoration: true,
    defaultPreloadStaleTime: 0,
  });

  return router;
};
