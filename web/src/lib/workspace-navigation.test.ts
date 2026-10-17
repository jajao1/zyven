import { describe, expect, it, vi } from 'vitest'
import { readWorkspaceLocation, navigateWorkspace } from './catalog-navigation'

describe('workspace navigation', () => {
  it.each([
    ['/dashboard?organization=org', 'overview', undefined, undefined],
    ['/products?organization=org', 'products', 'products', undefined],
    ['/offers/abc?organization=org', 'offers', 'offers', 'abc'],
    ['/customers?organization=org', 'customers', undefined, undefined],
    ['/team?organization=org', 'team', undefined, undefined],
    ['/settings?organization=org', 'settings', undefined, undefined],
    ['/sales?organization=org', 'sales', undefined, undefined],
  ])('parses %s', (url, view, kind, item) => {
    const parsed = readWorkspaceLocation(url)
    expect(parsed).toMatchObject({ org: 'org', view, kind, item })
  })

  it('navigates with an encoded organization and emits one location event', () => {
    const listener = vi.fn()
    window.addEventListener('popstate', listener)
    navigateWorkspace('/customers', 'org one')
    expect(window.location.pathname + window.location.search).toBe('/customers?organization=org+one')
    expect(listener).toHaveBeenCalledOnce()
    window.removeEventListener('popstate', listener)
  })
})
