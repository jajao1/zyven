import { describe, it, expect, afterEach } from 'vitest'
import { render, screen, fireEvent, waitFor, cleanup, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi } from 'vitest'
import { Catalog } from './Catalog'
import { offerSchema } from './lib/catalog-validation'
import { catalogClient } from './lib/catalog-client'
vi.mock('./lib/catalog-client', async original => ({ ...await original<typeof import('./lib/catalog-client')>(), catalogClient: { products: vi.fn(), product: vi.fn(), offers: vi.fn(), saveProduct: vi.fn(), saveOffer: vi.fn() } }))
afterEach(() => { cleanup(); vi.clearAllMocks(); window.history.replaceState(null, '', '/') })
function setup(role: 'OWNER' | 'SUPPORT' = 'OWNER') {
  window.history.replaceState(null, '', '/products?organization=org')
  vi.mocked(catalogClient.products).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 })
  vi.mocked(catalogClient.offers).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 })
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><Catalog org="org" userId="user" role={role} /></QueryClientProvider>)
}
describe('catalog', () => {
  it('creates a draft product with validated fields and returns to its list', async () => {
    setup(); await screen.findByText('Seu catálogo começa com um produto.'); fireEvent.click(screen.getByRole('button', { name: 'Novo produto' }))
    fireEvent.change(screen.getByLabelText('Nome do produto'), { target: { value: 'Course' } }); fireEvent.change(screen.getByLabelText('Slug do produto'), { target: { value: 'course' } })
    vi.mocked(catalogClient.saveProduct).mockResolvedValue({ id: 'abc', organizationId: 'org', name: 'Course', slug: 'course', description: '', imageUrl: '', status: 'DRAFT', createdAt: '', updatedAt: '' })
    fireEvent.click(screen.getByRole('button', { name: 'Criar produto' }))
    await waitFor(() => expect(catalogClient.saveProduct).toHaveBeenCalledWith('org', expect.objectContaining({ name: 'Course', status: 'DRAFT' }), undefined))
  })
  it('keeps support access read-only including direct new URLs', async () => {
    setup('SUPPORT'); await screen.findByText('Seu catálogo começa com um produto.'); expect(screen.queryByRole('button', { name: 'Novo produto' })).not.toBeInTheDocument()
    act(() => { window.history.pushState(null, '', '/products/new?organization=org'); window.dispatchEvent(new PopStateEvent('popstate')) })
    expect(await screen.findByText('Seu papel permite apenas consultar o catálogo.')).toBeInTheDocument()
  })
})


it('preserves exact money text and rejects rounding, overflow and trailing whitespace', () => {
  const input = { productId: 'product', name: 'Offer', slug: 'offer', headline: '', description: '', price: '9999999999999999.99', currency: 'BRL', billingType: 'ONE_TIME', status: 'DRAFT' }
  expect(offerSchema.parse(input).price).toBe('9999999999999999.99')
  for (const price of ['0', '0.00', '-1', '1.001', '19.90\n', '10000000000000000', '1e2']) expect(offerSchema.safeParse({ ...input, price }).success).toBe(false)
  expect(offerSchema.safeParse({ ...input, currency: 'BRL\n' }).success).toBe(false)
  expect(offerSchema.safeParse({ ...input, slug: 'offer\n' }).success).toBe(false)
})
it('shows price validation and submits offers as exact decimal strings', async () => {
  setup()
  const product = { id: 'abc', organizationId: 'org', name: 'Course', slug: 'course', description: '', imageUrl: '', status: 'DRAFT' as const, createdAt: '', updatedAt: '' }
  vi.mocked(catalogClient.products).mockResolvedValue({ items: [product], total: 1, page: 1, pageSize: 20 })
  vi.mocked(catalogClient.product).mockResolvedValue(product)
  await screen.findByText('Seu catálogo começa com um produto.')
  fireEvent.click(screen.getByRole('button', { name: 'Ofertas' })); fireEvent.click(await screen.findByRole('button', { name: 'Nova oferta' }))
  await screen.findByRole('option', { name: 'Course' })
  fireEvent.change(screen.getByLabelText('Produto da oferta'), { target: { value: 'abc' } })
  fireEvent.change(screen.getByLabelText('Nome da oferta'), { target: { value: 'Lifetime' } }); fireEvent.change(screen.getByLabelText('Slug da oferta'), { target: { value: 'lifetime' } })
  fireEvent.change(screen.getByLabelText('Preço'), { target: { value: '19.999' } }); fireEvent.click(screen.getByRole('button', { name: 'Criar oferta' }))
  expect(await screen.findByText('Use um valor com até duas casas decimais, separado por ponto.')).toBeInTheDocument(); expect(catalogClient.saveOffer).not.toHaveBeenCalled()
  vi.mocked(catalogClient.saveOffer).mockResolvedValue({ ...product, productId: 'abc', headline: '', price: '19.90', currency: 'BRL', billingType: 'ONE_TIME' })
  fireEvent.change(screen.getByLabelText('Preço'), { target: { value: '19.90' } }); fireEvent.click(screen.getByRole('button', { name: 'Criar oferta' }))
  await waitFor(() => expect(catalogClient.saveOffer).toHaveBeenCalledWith('org', expect.objectContaining({ price: '19.90', productId: 'abc' }), undefined))
  await waitFor(() => expect(window.location.pathname).toBe('/offers'))
})
it('responds to browser navigation and reads direct detail paths', async () => {
  setup()
  await screen.findByText('Seu catálogo começa com um produto.')
  vi.mocked(catalogClient.product).mockResolvedValue({ id: 'abc', organizationId: 'org', name: 'Saved course', slug: 'course', description: '', imageUrl: '', status: 'ACTIVE', createdAt: '', updatedAt: '' })
  act(() => { window.history.pushState(null, '', '/products/abc?organization=org'); window.dispatchEvent(new PopStateEvent('popstate')) })
  expect(await screen.findByDisplayValue('Saved course')).toBeInTheDocument()
  act(() => { window.history.replaceState(null, '', '/offers?organization=org'); window.dispatchEvent(new PopStateEvent('popstate')) })
  expect(await screen.findByText('Crie uma oferta para um produto do catálogo.')).toBeInTheDocument()
})
