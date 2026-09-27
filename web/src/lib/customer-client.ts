import { authorizedRequest } from './auth-client'
import type { Page } from './organization-client'

export interface Customer { id: string; name: string; email: string; phone?: string; document?: string; createdAt: string; updatedAt: string }
export const customerClient = {
  list: (org: string, page = 1) => authorizedRequest<Page<Customer>>(`/api/organizations/${org}/customers?page=${page}&pageSize=20`),
}
