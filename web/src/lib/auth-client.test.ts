import { beforeEach, describe, expect, it, vi } from 'vitest'
import { authClient } from './auth-client'

const session = { accessToken: 'access-secret', user: { id: 'id', email: 'test@example.com', displayName: 'Ana' } }
const ok = () => new Response(JSON.stringify(session), { status: 200 })

describe('auth client', () => {
  beforeEach(() => { authClient.clear(); vi.unstubAllGlobals() })

  it('coalesces concurrent refreshes so rotating cookies are never replayed in one tab', async () => {
    const fetch = vi.fn().mockResolvedValue(ok())
    vi.stubGlobal('fetch', fetch)
    const [a, b] = await Promise.all([authClient.refresh(), authClient.refresh()])
    expect(fetch).toHaveBeenCalledTimes(1)
    expect(a).toEqual(session)
    expect(b).toEqual(session)
    expect(fetch.mock.calls[0][1].credentials).toBe('same-origin')
    expect(fetch.mock.calls[0][1].headers['X-Zyven-Client']).toBe('web')
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('keeps the session when logout cannot reach the server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(ok()).mockRejectedValueOnce(new TypeError('offline')))
    await authClient.refresh()
    await expect(authClient.logout()).rejects.toThrow()
    expect(authClient.token()).toBe('access-secret')
  })

  it('clears memory after confirmed logout', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(ok()).mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetch)
    await authClient.refresh()
    await authClient.logout()
    expect(authClient.token()).toBeNull()
    expect(fetch.mock.calls[1][1].headers.Authorization).toBe('Bearer access-secret')
  })

  it('discards revoked credentials and never retries a failed refresh', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(ok()).mockResolvedValueOnce(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetch)
    await authClient.refresh()
    await expect(authClient.refresh()).rejects.toThrow()
    expect(authClient.token()).toBeNull()
    expect(fetch).toHaveBeenCalledTimes(2)
  })

  it('refreshes an expired access token before revoking the session on logout', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(ok())
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(ok())
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetch)
    await authClient.refresh()
    await authClient.logout()
    expect(fetch.mock.calls.map(call => call[0])).toEqual([
      '/api/auth/refresh', '/api/auth/logout', '/api/auth/refresh', '/api/auth/logout',
    ])
    expect(authClient.token()).toBeNull()
  })
})

it('retries concurrent organization requests once using a single refreshed credential', async () => {
  const module = await import('./auth-client')
  expect(module).toHaveProperty('authorizedRequest')
  let refreshes = 0
  vi.stubGlobal('fetch', vi.fn(async (path, init) => {
    if (path === '/api/auth/refresh') { refreshes++; return ok() }
    return init.headers.Authorization === 'Bearer access-secret'
      ? new Response(JSON.stringify({ name: 'Studio' })) : new Response(null, { status: 401 })
  }))
  authClient.clear()
  const result = await Promise.all([module.authorizedRequest('/api/organizations/a'), module.authorizedRequest('/api/organizations/b')])
  expect(refreshes).toBe(1)
  expect(result).toEqual([{ name: 'Studio' }, { name: 'Studio' }])
})

it('does not refresh permission failures or retry more than once', async () => {
  const module = await import('./auth-client')
  expect(module).toHaveProperty('authorizedRequest')
  const fetch = vi.fn(async (path) => path === '/api/auth/refresh' ? ok() : new Response(null, { status: 401 }))
  vi.stubGlobal('fetch', fetch)
  await expect(module.authorizedRequest('/api/organizations/a')).rejects.toMatchObject({ status: 401 })
  expect(fetch).toHaveBeenCalledTimes(3)
  fetch.mockClear().mockResolvedValue(new Response(null, { status: 403 }))
  await expect(module.authorizedRequest('/api/organizations/a')).rejects.toMatchObject({ status: 403 })
  expect(fetch).toHaveBeenCalledTimes(1)
})
