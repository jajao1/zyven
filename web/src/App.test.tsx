import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup } from '@testing-library/react'
import App from './App'
import { ApiError, authClient } from './lib/auth-client'

afterEach(cleanup)

function openApp() {
  vi.spyOn(authClient, 'refresh').mockRejectedValue(new ApiError(401, 'Expired'))
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><App /></QueryClientProvider>)
}

it('validates registration fields without sending invalid credentials', async () => {
  openApp()
  const register = vi.spyOn(authClient, 'register')
  fireEvent.click(await screen.findByRole('button', { name: 'Criar uma conta' }))
  fireEvent.change(screen.getByLabelText('Seu nome'), { target: { value: 'Ana' } })
  fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: 'ana@example.com' } })
  fireEvent.change(screen.getByLabelText('Senha'), { target: { value: '123' } })
  fireEvent.click(screen.getByRole('button', { name: 'Criar conta' }))
  await waitFor(() => expect(screen.getByLabelText('Senha')).toHaveAttribute('aria-invalid', 'true'))
  expect(await screen.findByText('Use de 12 a 128 caracteres. Uma frase longa é uma boa escolha.')).toBeInTheDocument()
  expect(register).not.toHaveBeenCalled()
})

it('shows the real account after login and confirms logout', async () => {
  openApp()
  const user = { id: '1', displayName: 'Ana', email: 'ana@example.com' }
  vi.spyOn(authClient, 'login').mockResolvedValue({ accessToken: 'token', user })
  vi.spyOn(authClient, 'me').mockResolvedValue(user)
  vi.spyOn(authClient, 'logout').mockResolvedValue()
  fireEvent.change(await screen.findByLabelText('E-mail'), { target: { value: user.email } })
  fireEvent.change(screen.getByLabelText('Senha'), { target: { value: 'Validpassword123!' } })
  fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))
  expect(await screen.findByRole('heading', { name: 'Olá, Ana.' })).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Sair da conta' }))
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Entre na sua conta.' })).toBeInTheDocument())
})

it('does not resurrect a logged-out account when an older session request completes', async () => {
  const user = { id: 'alice', displayName: 'Alice', email: 'alice@example.com' }
  let resolveMe!: (value: typeof user) => void
  const pending = new Promise<typeof user>(resolve => { resolveMe = resolve })
  vi.spyOn(authClient, 'token').mockReturnValue('access')
  vi.spyOn(authClient, 'me').mockReturnValue(pending)
  vi.spyOn(authClient, 'logout').mockResolvedValue()
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ items: [], page: 1, pageSize: 20, total: 0 }))))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  client.setQueryData(['session'], user)
  render(<QueryClientProvider client={client}><App /></QueryClientProvider>)
  const refetch = client.refetchQueries({ queryKey: ['session'] })
  fireEvent.click(screen.getByRole('button', { name: 'Sair da conta' }))
  await screen.findByRole('heading', { name: 'Entre na sua conta.' })
  await act(async () => { resolveMe(user); await refetch })
  expect(screen.getByRole('heading', { name: 'Entre na sua conta.' })).toBeInTheDocument()
  expect(client.getQueryData(['session'])).toBeNull()
  vi.unstubAllGlobals()
})

it('keeps an expired account organization cache out of the next account', async () => {
  const alice = { id: 'alice', displayName: 'Alice', email: 'alice@example.com' }
  const bob = { id: 'bob', displayName: 'Bob', email: 'bob@example.com' }
  const cached = { items: [{ id: 'private', name: 'Alice private studio', role: 'OWNER', createdAt: '', updatedAt: '' }], page: 1, pageSize: 20, total: 1 }
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  client.setQueryData(['session'], alice)
  client.setQueryData(['organizations', 'list', 1], cached)
  client.setQueryData(['organizations', 'alice', 'list', 1], cached)
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})))
  vi.spyOn(authClient, 'login').mockResolvedValue({ accessToken: 'bob-access', user: bob })
  render(<QueryClientProvider client={client}><App /></QueryClientProvider>)
  expect(screen.getByRole('button', { name: /Alice private studio/ })).toBeInTheDocument()
  await act(async () => { client.setQueryData(['session'], null) })
  fireEvent.change(await screen.findByLabelText('E-mail'), { target: { value: bob.email } })
  fireEvent.change(screen.getByLabelText('Senha'), { target: { value: 'Validpassword123!' } })
  fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))
  await screen.findByRole('heading', { name: 'Olá, Bob.' })
  expect(screen.queryByRole('button', { name: /Alice private studio/ })).not.toBeInTheDocument()
  vi.unstubAllGlobals()
})
