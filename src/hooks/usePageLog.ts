import { useEffect } from "react";
import { mauiTrace } from "@/native/mauiWebberClient";

export function usePageLog(page: string) {
  useEffect(() => {
    mauiTrace("page.render", {
      page,
      href: window.location.href,
      hash: window.location.hash,
      pathname: window.location.pathname,
    });
  }, [page]);
}
