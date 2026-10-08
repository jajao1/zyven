import { ApiError } from './auth-client'

export interface BuyerProfile { email: string }
export interface BuyerPurchase { id: string; offerName: string; sellerName: string; currency: string; amount: string; paidAt: string; accessStatus: string }
export interface BuyerPurchaseDetail extends BuyerPurchase { items: { id: string; type: string; name: string; url: string; deliveredAt: string }[] }

async function request<T>(path: string, body?: unknown, method = 'GET'): Promise<T> {
  const response = await fetch(`/api/buyer/${path}`, { method, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-Zyven-Client': 'web' }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) })
  if (!response.ok) throw new ApiError(response.status, response.status === 401 ? 'Código inválido, expirado ou sessão encerrada.' : response.status === 429 ? 'Muitas tentativas. Aguarde alguns minutos.' : 'Não foi possível concluir. Tente novamente.')
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

export const buyerClient = {
  requestCode: (email: string) => request<{ message: string }>('auth/request-code', { email }, 'POST'),
  verifyCode: (email: string, code: string) => request<BuyerProfile>('auth/verify-code', { email, code }, 'POST'),
  me: () => request<BuyerProfile>('me'),
  logout: () => request<void>('auth/logout', undefined, 'POST'),
  purchases: () => request<BuyerPurchase[]>('purchases'),
  detail: (id: string) => request<BuyerPurchaseDetail>(`purchases/${id}`),
}
