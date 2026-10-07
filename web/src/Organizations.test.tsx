import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import App from './App'

vi.mock('./Catalog', () => ({ Catalog: () => <div>Catálogo carregado</div> }))
const user = { id: 'u1', displayName: 'Ana', email: 'ana@example.com' }
const first = { id: 'a', name: 'Studio A', role: 'OWNER', createdAt: '', updatedAt: '' }
const second = { ...first, id: 'b', name: 'Studio B', role: 'SUPPORT' }
afterEach(() => { cleanup(); vi.unstubAllGlobals(); window.history.replaceState(null, '', '/') })
function open(list = [first, second]) {
  const fetch = vi.fn(async (path: string, init?: RequestInit) => {
    if (path.startsWith('/api/organizations?') || path === '/api/organizations') return new Response(JSON.stringify(init?.method === 'POST' ? { ...first, id: 'c', name: 'Novo espaço' } : { items: list, total: list.length, page: 1, pageSize: 20 }))
    if (/\/(products|offers|customers)\?/.test(path)) return new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }))
    if (path.includes('/members?')) return new Response(JSON.stringify({ items: [{ id: path.includes('/a/') ? 'ma' : 'mb', userId: 'u1', email: user.email, displayName: path.includes('/a/') ? 'Equipe A' : 'Equipe B', role: path.includes('/a/') ? 'OWNER' : 'SUPPORT', createdAt: '' }], total: 1, page: 1, pageSize: 20 }))
    if (path.endsWith('/payment-account')) return new Response(JSON.stringify({ status: 'ACTIVE', provider: 'PUSHINPAY', tokenFingerprint: 'ABCDEF123456' }))
    return new Response(JSON.stringify(list.find(item => path.endsWith('/' + item.id)) ?? first))
  })
  vi.stubGlobal('fetch', fetch)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); client.setQueryData(['session'], user)
  render(<QueryClientProvider client={client}><App /></QueryClientProvider>)
  return { fetch }
}

it('opens a selected organization on the truthful overview and keeps catalog routes', async () => {
  open()
  fireEvent.click(await screen.findByRole('button', { name: /Studio A/ }))
  expect(await screen.findByRole('heading', { name: 'Studio A' })).toBeInTheDocument()
  expect(screen.getByLabelText('Navegação da organização')).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: /Produtos/ }))
  expect(await screen.findByText('Catálogo carregado')).toBeInTheDocument()
  expect(window.location.pathname + window.location.search).toBe('/products?organization=a')
})

it('creates an organization and lands on its overview', async () => {
  const { fetch } = open([])
  fireEvent.change(await screen.findByLabelText('Nova organização'), { target: { value: 'Novo espaço' } })
  fireEvent.click(screen.getByRole('button', { name: 'Criar' }))
  await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/organizations', expect.objectContaining({ method: 'POST', body: JSON.stringify({ name: 'Novo espaço' }) })))
  expect(window.location.pathname).toBe('/dashboard')
})

it('manages members only from the team destination', async () => {
  const { fetch } = open([first])
  fireEvent.click(await screen.findByRole('button', { name: /Studio A/ }))
  fireEvent.click(await screen.findByRole('button', { name: /Equipe/ }))
  await screen.findByText('Equipe A')
  fireEvent.change(screen.getByLabelText('E-mail da pessoa'), { target: { value: 'nova@example.com' } })
  fireEvent.change(screen.getByLabelText('Papel'), { target: { value: 'FINANCE' } })
  fireEvent.click(screen.getByRole('button', { name: 'Adicionar membro' }))
  await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/organizations/a/members', expect.objectContaining({ method: 'POST', body: JSON.stringify({ email: 'nova@example.com', role: 'FINANCE' }) })))
})

it('keeps support members read-only', async () => {
  open([second])
  fireEvent.click(await screen.findByRole('button', { name: /Studio B/ }))
  fireEvent.click(await screen.findByRole('button', { name: /Equipe/ }))
  expect(await screen.findByText('Equipe B')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Adicionar membro' })).not.toBeInTheDocument()
})

it('connects PushinPay without retaining the submitted token', async () => {
  const { fetch } = open([first])
  fireEvent.click(await screen.findByRole('button', { name: /Studio A/ }))
  fireEvent.click(await screen.findByRole('button', { name: /Configurações/ }))
  fireEvent.click(await screen.findByRole('button', { name: /Pagamentos e gateway/ }))
  fireEvent.change(await screen.findByLabelText('Token PushinPay'), { target: { value: 'seller-secret' } })
  fireEvent.click(screen.getByRole('button', { name: 'Conectar PushinPay' }))
  expect(await screen.findByText(/PushinPay conectada/)).toBeInTheDocument()
  expect(screen.queryByDisplayValue('seller-secret')).not.toBeInTheDocument()
  expect(fetch).toHaveBeenCalledWith('/api/organizations/a/payment-account', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ token: 'seller-secret' }) }))
})
