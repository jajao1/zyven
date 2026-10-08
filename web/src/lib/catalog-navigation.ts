import { useSyncExternalStore } from 'react'
const subscribe = (callback: () => void) => { window.addEventListener('popstate', callback); return () => window.removeEventListener('popstate', callback) }
export type WorkspaceView = 'overview' | 'products' | 'offers' | 'customers' | 'sales' | 'team' | 'settings' | 'organizations'
export interface WorkspaceLocation { view: WorkspaceView; kind?: 'products' | 'offers'; item?: string; org: string }
export function navigateWorkspace(path: string, org: string) {
  const query = new URLSearchParams()
  if (org) query.set('organization', org)
  window.history.pushState(null, '', query.size ? `${path}?${query}` : path)
  window.dispatchEvent(new PopStateEvent('popstate'))
}
export const navigateCatalog = navigateWorkspace
export function readWorkspaceLocation(location: string): WorkspaceLocation {
  const [path, query = ''] = location.split('?')
  const catalog = /^\/(products|offers)(?:\/(new|[a-f0-9-]+))?$/.exec(path)
  const simple = /^\/(dashboard|customers|sales|team|settings)\/?$/.exec(path)?.[1]
  const view: WorkspaceView = (catalog?.[1] as 'products' | 'offers' | undefined) ?? (simple === 'dashboard' ? 'overview' : simple as WorkspaceView | undefined) ?? 'organizations'
  return { view, kind: catalog?.[1] as 'products' | 'offers' | undefined, item: catalog?.[2], org: new URLSearchParams(query).get('organization') ?? '' }
}
export function useCatalogLocation() {
  const location = useSyncExternalStore(subscribe, () => window.location.pathname + window.location.search)
  return readWorkspaceLocation(location)
}
