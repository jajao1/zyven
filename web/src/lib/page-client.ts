import { ApiError, authorizedRequest } from './auth-client'
export interface CheckoutField { key: string; label: string; type: 'text' | 'textarea'; required: boolean }
export interface PageContent { title: string; subtitle: string; imageUrl?: string | null; videoUrl?: string | null; logoUrl?: string | null; color: string; description: string; benefits: string[]; testimonials: { name: string; text: string }[]; faq: { question: string; answer: string }[]; guarantee: string; cta: string; fields: CheckoutField[] }
export interface PublicOffer { slug: string; name: string; productName: string; price: string; currency: string; billingType: string; page: PageContent }
export interface BuyerInput { name: string; email: string; phone: string; document: string; fields: Record<string, string> }
export interface Checkout { id: string; offerSlug: string; status: string; price: string; currency: string; expiresAt: string; name: string; email: string; phone?: string; document?: string; fields: Record<string, string> }
async function request<T>(url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, { method: body === undefined ? 'GET' : 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-Zyven-Client': 'web' }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) })
  if (!response.ok) { const problem = await response.json().catch(() => ({})); throw new ApiError(response.status, response.status === 429 ? 'Muitas tentativas. Aguarde alguns minutos.' : problem.title ?? 'Esta oferta ou checkout não está disponível.') }
  return response.json() as Promise<T>
}
export const pageClient = {
  get: (org: string, id: string) => authorizedRequest<PageContent>(`/api/organizations/${org}/offers/${id}/page`),
  save: (org: string, id: string, data: PageContent) => authorizedRequest<PageContent>(`/api/organizations/${org}/offers/${id}/page`, data, 'PUT'),
  offer: (slug: string) => request<PublicOffer>(`/api/public/offers/${encodeURIComponent(slug)}`),
  create: (slug: string, data: BuyerInput) => request<Checkout>(`/api/public/offers/${encodeURIComponent(slug)}/checkouts`, data),
  checkout: (id: string) => request<Checkout>(`/api/public/checkouts/${encodeURIComponent(id)}`),
}
