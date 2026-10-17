export interface User { id: string; email: string; displayName: string }
export interface Session { accessToken: string; user: User }

export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) { super(message); this.status = status }
}

let accessToken: string | null = null
let refreshPending: Promise<Session> | null = null

async function request<T>(path: string, body?: unknown, method = 'POST'): Promise<T> {
  const response = await fetch(`/api/auth/${path}`, {
    method,
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      'X-Zyven-Client': 'web',
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })
  if (!response.ok) {
    const message = response.status === 429 ? 'Muitas tentativas. Aguarde alguns minutos e tente novamente.'
      : response.status === 401 ? 'E-mail ou senha incorretos, ou sessão expirada.'
      : response.status === 409 ? 'Não foi possível criar a conta com este e-mail.'
      : response.status === 400 ? 'Confira os dados informados e tente novamente.'
      : 'Não foi possível concluir. Tente novamente em instantes.'
    throw new ApiError(response.status, message)
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

function remember(session: Session) {
  accessToken = session.accessToken
  return session
}

async function rotate() {
  try { return remember(await request<Session>('refresh')) }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) accessToken = null
    throw error
  }
}

export const authClient = {
  clear() { accessToken = null },
  token() { return accessToken },
  async login(data: { email: string; password: string }) {
    return remember(await request<Session>('login', data))
  },
  async register(data: { email: string; password: string; displayName: string }) {
    return remember(await request<Session>('register', data))
  },
  refresh(): Promise<Session> {
    // One rotation per tab; Web Locks also serialize tabs sharing the HttpOnly cookie.
    refreshPending ??= (navigator.locks
      ? navigator.locks.request('zyven-session-rotation', rotate)
      : rotate()).finally(() => { refreshPending = null })
    return refreshPending
  },
  async me(): Promise<User> {
    try { return await request<User>('me', undefined, 'GET') }
    catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error
      return (await this.refresh()).user
    }
  },
  async logout(): Promise<void> {
    try { await request<void>('logout') }
    catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error
      try { await this.refresh() }
      catch (refreshError) {
        if (!(refreshError instanceof ApiError) || refreshError.status !== 401) throw refreshError
        // Server confirmed that both credentials are inactive and removed the cookie.
        accessToken = null
        return
      }
      await request<void>('logout')
    }
    accessToken = null
  },
}

export async function authorizedRequest<T>(path: string, body?: unknown, method = 'GET'): Promise<T> {
  const usedToken = accessToken
  const send = () => fetch(path, {
    method, credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-Zyven-Client': 'web', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })
  let response = await send()
  if (response.status === 401) {
    // A slower 401 may arrive after another request has already rotated the token.
    if (accessToken === usedToken) await authClient.refresh()
    response = await send()
  }
  if (!response.ok) {
    let title: string | undefined
    try { title = (await response.json() as { title?: string }).title } catch { /* empty response */ }
    throw new ApiError(response.status, title ?? (response.status === 401 ? 'Sua sessão expirou. Entre novamente.' : 'Não foi possível concluir. Tente novamente.'))
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

export async function authorizedFormRequest<T>(path: string, body: FormData): Promise<T> {
  const send = () => fetch(path, { method: 'POST', credentials: 'same-origin', headers: { 'X-Zyven-Client': 'web', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) }, body })
  let response = await send()
  if (response.status === 401) { await authClient.refresh(); response = await send() }
  if (!response.ok) { let title: string | undefined; try { title = (await response.json() as { title?: string }).title } catch { /* empty */ } throw new ApiError(response.status, title ?? 'Não foi possível enviar o arquivo.') }
  return response.json() as Promise<T>
}
