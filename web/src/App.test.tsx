import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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
