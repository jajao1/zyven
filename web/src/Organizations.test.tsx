import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import App from './App'

const user = { id: 'u1', displayName: 'Ana', email: 'ana@example.com' }
const first = { id: 'a', name: 'Studio A', role: 'OWNER', createdAt: '', updatedAt: '' }
const second = { ...first, id: 'b', name: 'Studio B', role: 'SUPPORT' }
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
function open(list = [first, second]) {
  const fetch = vi.fn(async (path: string, init?: RequestInit) => {
    if (path.startsWith('/api/organizations?') || path === '/api/organizations') return new Response(JSON.stringify(init?.method === 'POST' ? { ...first, id: 'c', name: 'Novo espaço' } : { items: list, total: list.length, page: 1, pageSize: 20 }))
    if (path.includes('/members?')) return new Response(JSON.stringify({ items: [{ id: path.includes('/a/') ? 'ma' : 'mb', userId: 'u1', email: user.email, displayName: path.includes('/a/') ? 'Equipe A' : 'Equipe B', role: path.includes('/a/') ? 'OWNER' : 'SUPPORT', createdAt: '' }], total: 1, page: 1, pageSize: 20 }))
    return new Response(JSON.stringify(list.find(item => path.endsWith('/' + item.id)) ?? first))
  })
  vi.stubGlobal('fetch', fetch)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  client.setQueryData(['session'], user)
  render(<QueryClientProvider client={client}><App /></QueryClientProvider>)
  return { client, fetch }
}
it('switches tenant details and team without leaking cached members and respects read-only roles', async () => {
  open()
  expect(await screen.findByRole('heading', { name: 'Suas organizações' })).toBeInTheDocument()
  fireEvent.change(await screen.findByLabelText('Organização ativa'), { target: { value: 'a' } })
  expect(await screen.findByText('Equipe A')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Adicionar membro' })).toBeInTheDocument()
  fireEvent.change(await screen.findByLabelText('Organização ativa'), { target: { value: 'b' } })
  expect(await screen.findByText('Equipe B')).toBeInTheDocument()
  expect(screen.queryByText('Equipe A')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Adicionar membro' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Salvar nome' })).not.toBeInTheDocument()
})
it('shows the empty state and creates an organization with the entered name', async () => {
  const { fetch } = open([])
  expect(await screen.findByText('Você ainda não participa de uma organização.')).toBeInTheDocument()
  fireEvent.change(screen.getByLabelText('Nome da nova organização'), { target: { value: 'Novo espaço' } })
  fireEvent.click(screen.getByRole('button', { name: 'Criar organização' }))
  await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/organizations', expect.objectContaining({ method: 'POST', body: JSON.stringify({ name: 'Novo espaço' }) })))
})



it('sends member additions to the selected organization and displays server ownership protection', async () => {
  const { fetch } = open()
  const base = fetch.getMockImplementation()!
  fetch.mockImplementation(async (path, init) => {
    if (path === '/api/organizations/a/members/ma' && init?.method === 'DELETE') return new Response(JSON.stringify({ title: 'A organização precisa de pelo menos um proprietário.' }), { status: 409 })
    return base(path, init)
  })
  fireEvent.change(await screen.findByLabelText('Organização ativa'), { target: { value: 'a' } })
  await screen.findByText('Equipe A')
  fireEvent.change(screen.getByLabelText('E-mail da pessoa'), { target: { value: 'nova@example.com' } })
  fireEvent.change(screen.getByLabelText('Papel'), { target: { value: 'FINANCE' } })
  fireEvent.click(screen.getByRole('button', { name: 'Adicionar membro' }))
  await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/organizations/a/members', expect.objectContaining({ method: 'POST', body: JSON.stringify({ email: 'nova@example.com', role: 'FINANCE' }) })))
  await waitFor(() => expect(screen.getByLabelText('E-mail da pessoa')).toHaveValue(''))
  fireEvent.click(screen.getByRole('button', { name: 'Remover' }))
  expect(fetch).not.toHaveBeenCalledWith('/api/organizations/a/members/ma', expect.objectContaining({ method: 'DELETE' }))
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar remoção' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('pelo menos um proprietário')
  expect(screen.getByText('Equipe A')).toBeInTheDocument()
})


it('limits administrator role choices and prevents controls on owner rows', async () => {
  open([{ ...first, role: 'ADMIN' }])
  fireEvent.change(await screen.findByLabelText('Organização ativa'), { target: { value: 'a' } })
  await screen.findByText('Equipe A')
  expect(screen.getByLabelText('Papel').querySelectorAll('option')).toHaveLength(3)
  expect(screen.queryByLabelText('Papel de Equipe A')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Remover' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Salvar nome' })).toBeInTheDocument()
})


it('drops a privileged draft role when the current owner becomes an administrator', async () => {
  const organization = { ...first }
  const { client, fetch } = open([organization])
  fireEvent.change(await screen.findByLabelText('Organização ativa'), { target: { value: 'a' } })
  await screen.findByText('Equipe A')
  fireEvent.change(screen.getByLabelText('Papel'), { target: { value: 'OWNER' } })
  organization.role = 'ADMIN'
  await act(async () => { await client.invalidateQueries({ queryKey: ['organizations'] }) })
  fireEvent.change(screen.getByLabelText('E-mail da pessoa'), { target: { value: 'new@example.com' } })
  fireEvent.click(screen.getByRole('button', { name: 'Adicionar membro' }))
  await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/organizations/a/members', expect.objectContaining({ method: 'POST', body: JSON.stringify({ email: 'new@example.com', role: 'OPERATOR' }) })))
})
