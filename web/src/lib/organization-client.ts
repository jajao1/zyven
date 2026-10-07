import { authorizedRequest } from './auth-client'
export type Role = 'OWNER' | 'ADMIN' | 'OPERATOR' | 'FINANCE' | 'SUPPORT'
export interface Organization { id: string; name: string; role: Role; createdAt: string; updatedAt: string }
export interface Member { id: string; userId: string; email: string; displayName: string; role: Role; createdAt: string }
export interface Page<T> { items: T[]; page: number; pageSize: number; total: number }
export interface PaymentAccount { status: string; provider: string; tokenFingerprint: string }
export const roleNames: Record<Role, string> = { OWNER: 'Proprietário', ADMIN: 'Administrador', OPERATOR: 'Operador', FINANCE: 'Financeiro', SUPPORT: 'Suporte' }
export const organizationClient = {
  list: (page: number) => authorizedRequest<Page<Organization>>(`/api/organizations?page=${page}&pageSize=20`),
  create: (name: string) => authorizedRequest<Organization>('/api/organizations', { name }, 'POST'),
  get: (id: string) => authorizedRequest<Organization>(`/api/organizations/${id}`),
  rename: (id: string, name: string) => authorizedRequest<Organization>(`/api/organizations/${id}`, { name }, 'PATCH'),
  members: (id: string, page: number) => authorizedRequest<Page<Member>>(`/api/organizations/${id}/members?page=${page}&pageSize=20`),
  add: (id: string, email: string, role: Role) => authorizedRequest<Member>(`/api/organizations/${id}/members`, { email, role }, 'POST'),
  change: (id: string, memberId: string, role: Role) => authorizedRequest<Member>(`/api/organizations/${id}/members/${memberId}`, { role }, 'PATCH'),
  remove: (id: string, memberId: string) => authorizedRequest<void>(`/api/organizations/${id}/members/${memberId}`, undefined, 'DELETE'),
  connectPayment: (id: string, token: string) => authorizedRequest<PaymentAccount>(`/api/organizations/${id}/payment-account`, { token }, 'PUT'),
}
