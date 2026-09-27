import { cleanup, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { Customers } from './Customers'
import { customerClient } from './lib/customer-client'

vi.mock('./lib/customer-client', () => ({ customerClient: { list: vi.fn() } }))
function open() { return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><Customers org="org" userId="user" /></QueryClientProvider>) }
beforeEach(() => vi.resetAllMocks())
afterEach(cleanup)

it('shows a truthful empty state', async () => {
  vi.mocked(customerClient.list).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 })
  open()
  expect(await screen.findByText('Nenhum cliente ainda')).toBeInTheDocument()
  expect(screen.queryByText(/vendas/i)).not.toBeInTheDocument()
})

it('renders real customer contact data', async () => {
  vi.mocked(customerClient.list).mockResolvedValue({ items: [{ id: 'c', name: 'Cliente real', email: 'cliente@example.test', phone: '+5511999990000', createdAt: '2026-09-27T12:00:00Z', updatedAt: '2026-09-27T12:00:00Z' }], total: 1, page: 1, pageSize: 20 })
  open()
  expect(await screen.findByText('Cliente real')).toBeInTheDocument()
  expect(screen.getByText('cliente@example.test')).toBeInTheDocument()
  expect(screen.getByText('+5511999990000')).toBeInTheDocument()
  expect(screen.getByText('cliente identificado').parentElement).toHaveTextContent('1cliente identificado')
})
