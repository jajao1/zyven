import { useSyncExternalStore } from 'react'
const subscribe = (callback: () => void) => { window.addEventListener('popstate', callback); return () => window.removeEventListener('popstate', callback) }
export function navigateCatalog(path: string, org: string) { window.history.pushState(null, '', `${path}?organization=${encodeURIComponent(org)}`); window.dispatchEvent(new PopStateEvent('popstate')) }
export function useCatalogLocation() {
  const location = useSyncExternalStore(subscribe, () => window.location.pathname + window.location.search)
  const [path, query] = location.split('?')
  const match = /^\/(products|offers)(?:\/(new|[a-f0-9-]+))?$/.exec(path)
  return { kind: match?.[1] as 'products' | 'offers' | undefined, item: match?.[2], org: new URLSearchParams(query).get('organization') ?? '' }
}
