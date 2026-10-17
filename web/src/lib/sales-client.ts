import { authorizedRequest } from './auth-client'
import type { Page } from './organization-client'

export interface SalesSummary { currency: string; availableBalance: string; totalReceived: string; totalFees: string; netPaid: string; totalPayments: number; paidPayments: number; pendingPayments: number; expiredPayments: number; failedPayments: number }
export interface Sale { id: string; status: string; customerName: string; customerEmail: string; offerName: string; paymentMethod: string; provider?: string; currency: string; grossAmount: string; platformFee: string; providerFee: string; netAmount: string; createdAt: string; paidAt?: string }
export interface SaleDetail extends Sale { checkoutId: string; customerId: string; offerId: string; externalReference: string; providerTransactionId?: string; endToEndId?: string; expiresAt: string; entitlementStatus?: string; fulfillmentStatus?: string }
export interface LedgerEntry { accountCode: string; debit: string; credit: string }
export interface LedgerTransaction { id: string; paymentId: string; type: string; reference: string; currency: string; occurredAt: string; entries: LedgerEntry[] }

export const salesClient = {
  summary: (org: string) => authorizedRequest<SalesSummary>(`/api/organizations/${org}/finance/summary`),
  sales: (org: string, page: number, status: string) => authorizedRequest<Page<Sale>>(`/api/organizations/${org}/finance/sales?page=${page}&pageSize=20${status ? `&status=${encodeURIComponent(status)}` : ''}`),
  detail: (org: string, id: string) => authorizedRequest<SaleDetail>(`/api/organizations/${org}/finance/sales/${id}`),
  ledger: (org: string, page: number) => authorizedRequest<Page<LedgerTransaction>>(`/api/organizations/${org}/finance/ledger?page=${page}&pageSize=20`),
}
