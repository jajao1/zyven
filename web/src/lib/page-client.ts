import { ApiError, authorizedFormRequest, authorizedRequest } from './auth-client'
export interface CheckoutField { key: string; label: string; type: 'text' | 'textarea'; required: boolean }
export interface PageContent { title: string; subtitle: string; imageUrl?: string | null; videoUrl?: string | null; logoUrl?: string | null; color: string; description: string; benefits: string[]; testimonials: { name: string; text: string }[]; faq: { question: string; answer: string }[]; guarantee: string; cta: string; fields: CheckoutField[] }
export interface PublicOffer { slug: string; name: string; productName: string; price: string; currency: string; billingType: string; page: PageContent }
export interface BuyerInput { name: string; email: string; phone: string; document: string; fields: Record<string, string> }
export interface Checkout { id: string; offerSlug: string; status: string; price: string; currency: string; expiresAt: string; name: string; email: string; phone?: string; document?: string; fields: Record<string, string> }
export interface PixPayment { id: string; status: string; amount: string; currency: string; pixCode?: string | null; qrCodeData?: string | null; expiresAt: string; paidAt?: string | null }
export interface Delivery { entitlementId: string; status: string; items: { id: string; type: string; name: string; url: string; deliveredAt: string }[] }
export interface ExternalLinkFulfillment { id: string; type: 'EXTERNAL_LINK'; name: string; url: string; status: string }
export interface DigitalFileFulfillment { id: string; type: 'DIGITAL_FILE'; name: string; contentType: string; size: number; status: string }
async function request<T>(url: string, body?: unknown, method?: string): Promise<T> {
  const actualMethod = method ?? (body === undefined ? 'GET' : 'POST')
  const response = await fetch(url, { method: actualMethod, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-Zyven-Client': 'web' }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) })
  if (!response.ok) { const problem = await response.json().catch(() => ({})); throw new ApiError(response.status, response.status === 429 ? 'Muitas tentativas. Aguarde alguns minutos.' : problem.title ?? 'Esta oferta ou checkout não está disponível.') }
  return response.json() as Promise<T>
}
export const pageClient = {
  get: (org: string, id: string) => authorizedRequest<PageContent>(`/api/organizations/${org}/offers/${id}/page`),
  save: (org: string, id: string, data: PageContent) => authorizedRequest<PageContent>(`/api/organizations/${org}/offers/${id}/page`, data, 'PUT'),
  externalLink: (org: string, id: string) => authorizedRequest<ExternalLinkFulfillment>(`/api/organizations/${org}/offers/${id}/fulfillments/external-link`),
  saveExternalLink: (org: string, id: string, data: { name: string; url: string }) => authorizedRequest<ExternalLinkFulfillment>(`/api/organizations/${org}/offers/${id}/fulfillments/external-link`, data, 'PUT'),
  digitalFile: (org: string, id: string) => authorizedRequest<DigitalFileFulfillment>(`/api/organizations/${org}/offers/${id}/fulfillments/digital-file`),
  saveDigitalFile: (org: string, id: string, file: File) => { const body = new FormData(); body.append('file', file); return authorizedFormRequest<DigitalFileFulfillment>(`/api/organizations/${org}/offers/${id}/fulfillments/digital-file`, body) },
  offer: (slug: string) => request<PublicOffer>(`/api/public/offers/${encodeURIComponent(slug)}`),
  create: (slug: string, data: BuyerInput) => request<Checkout>(`/api/public/offers/${encodeURIComponent(slug)}/checkouts`, data),
  checkout: (id: string) => request<Checkout>(`/api/public/checkouts/${encodeURIComponent(id)}`),
  createPix: (id: string) => request<PixPayment>(`/api/public/checkouts/${encodeURIComponent(id)}/payments/pix`, undefined, 'POST'),
  pix: (id: string) => request<PixPayment>(`/api/public/checkouts/${encodeURIComponent(id)}/payments/pix`),
  delivery: (id: string) => request<Delivery>(`/api/public/checkouts/${encodeURIComponent(id)}/delivery`),
}
