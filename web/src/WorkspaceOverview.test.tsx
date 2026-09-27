import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { WorkspaceOverview } from './WorkspaceOverview'
import { catalogClient } from './lib/catalog-client'
import { customerClient } from './lib/customer-client'

vi.mock('./lib/catalog-client', () => ({ catalogClient: { products: vi.fn(), offers: vi.fn() } }))
vi.mock('./lib/customer-client', () => ({ customerClient: { list: vi.fn() } }))
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 20 })
function open() { return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><WorkspaceOverview org="org" userId="user" organizationName="Estúdio" /></QueryClientProvider>) }

beforeEach(() => { vi.mocked(catalogClient.products).mockResolvedValue(page(2)); vi.mocked(catalogClient.offers).mockResolvedValue(page(1)); vi.mocked(customerClient.list).mockResolvedValue(page(3)); window.history.replaceState(null, '', '/') })
afterEach(cleanup)

it('shows real totals and keeps revenue unavailable', async () => {
  open()
  expect(await screen.findByRole('region', { name: 'Resumo da organização' })).toHaveTextContent('Produtos2Ofertas1Clientes3Receita—Disponível após pagamentos')
  expect(screen.getByText('2/3')).toBeInTheDocument()
  expect(screen.getByText('Ainda não configurado')).toBeInTheDocument()
})

it('routes onboarding actions inside the active organization', async () => {
  open()
  await screen.findByRole('region', { name: 'Resumo da organização' })
  fireEvent.click(screen.getAllByRole('button', { name: /Continuar/ })[0])
  expect(window.location.pathname + window.location.search).toBe('/offers?organization=org')
})
