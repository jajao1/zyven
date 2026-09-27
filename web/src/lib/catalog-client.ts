import { authorizedRequest } from './auth-client'
import type { Page } from './organization-client'
export type CatalogStatus = 'DRAFT' | 'ACTIVE' | 'INACTIVE' | 'ARCHIVED'
export const statusNames: Record<CatalogStatus, string> = { DRAFT: 'Rascunho', ACTIVE: 'Ativo', INACTIVE: 'Inativo', ARCHIVED: 'Arquivado' }
export interface ProductInput { name: string; slug: string; description: string; imageUrl: string; status: CatalogStatus }
export interface Product extends ProductInput { id: string; organizationId: string; createdAt: string; updatedAt: string }
export interface OfferInput { productId: string; name: string; slug: string; headline: string; description: string; price: string; currency: string; billingType: 'ONE_TIME' | 'SUBSCRIPTION'; status: CatalogStatus }
export interface Offer extends OfferInput { id: string; organizationId: string; createdAt: string; updatedAt: string }
export const catalogClient = {
  products: (org: string, page = 1) => authorizedRequest<Page<Product>>(`/api/organizations/${org}/products?page=${page}&pageSize=20`),
  product: (org: string, id: string) => authorizedRequest<Product>(`/api/organizations/${org}/products/${id}`),
  saveProduct: (org: string, data: ProductInput, id?: string) => authorizedRequest<Product>(`/api/organizations/${org}/products${id ? `/${id}` : ''}`, data, id ? 'PATCH' : 'POST'),
  offers: (org: string, page = 1) => authorizedRequest<Page<Offer>>(`/api/organizations/${org}/offers?page=${page}&pageSize=20`),
  offer: (org: string, id: string) => authorizedRequest<Offer>(`/api/organizations/${org}/offers/${id}`),
  saveOffer: (org: string, data: OfferInput, id?: string) => authorizedRequest<Offer>(`/api/organizations/${org}/offers${id ? `/${id}` : ''}`, data, id ? 'PATCH' : 'POST'),
}
