import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import { Sales } from './Sales'

afterEach(() => { cleanup(); vi.unstubAllGlobals() })
const summary = { currency: 'BRL', availableBalance: '19.40', totalReceived: '19.90', totalFees: '0.50', netPaid: '19.40', totalPayments: 1, paidPayments: 1, pendingPayments: 0, expiredPayments: 0, failedPayments: 0 }
const sale = { id: 'p1', status: 'PAID', customerName: 'Cliente Teste', customerEmail: 'cliente@example.test', offerName: 'Curso completo', paymentMethod: 'PIX', provider: 'PUSHINPAY', currency: 'BRL', grossAmount: '19.90', platformFee: '0.50', providerFee: '0.00', netAmount: '19.40', createdAt: '2026-10-07T12:00:00Z', paidAt: '2026-10-07T12:01:00Z' }
function open() {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path.endsWith('/summary')) return new Response(JSON.stringify(summary))
    if (path.endsWith('/sales/p1')) return new Response(JSON.stringify({ ...sale, checkoutId: 'c1', customerId: 'u1', offerId: 'o1', externalReference: 'ref', providerTransactionId: 'tx', endToEndId: 'e2e', expiresAt: '2026-10-07T13:00:00Z', entitlementStatus: 'ACTIVE', fulfillmentStatus: 'COMPLETED' }))
    if (path.includes('/ledger')) return new Response(JSON.stringify({ items: [{ id: 'l1', paymentId: 'p1', type: 'PAYMENT_CAPTURED', reference: 'ref', currency: 'BRL', occurredAt: sale.paidAt, entries: [{ accountCode: 'MERCHANT_AVAILABLE', debit: '0.00', credit: '19.40' }] }], page: 1, pageSize: 20, total: 1 }))
    return new Response(JSON.stringify({ items: [sale], page: 1, pageSize: 20, total: 1 }))
  }))
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><Sales org="org" userId="user" /></QueryClientProvider>)
}

it('shows financial metrics and opens a sale detail', async () => {
  open()
  expect(await screen.findByText((_, element) => element?.tagName === 'STRONG' && element.textContent?.includes('19,40') === true)).toBeInTheDocument()
  fireEvent.click(await screen.findByRole('button', { name: /Cliente Teste/ }))
  expect(await screen.findByText('COMPLETED')).toBeInTheDocument()
  expect(screen.getByLabelText('Detalhes da venda')).toHaveTextContent('COMPLETED')
  expect(screen.getByLabelText('Detalhes da venda')).toHaveTextContent('cliente@example.test')
})

it('loads immutable ledger entries from the extrato tab', async () => {
  open()
  fireEvent.click(await screen.findByRole('button', { name: 'Extrato' }))
  fireEvent.click(await screen.findByText('Pagamento confirmado'))
  expect((await screen.findAllByText('Saldo disponível')).length).toBeGreaterThan(1)
  expect(screen.getByText('MERCHANT_AVAILABLE')).toBeInTheDocument()
})
