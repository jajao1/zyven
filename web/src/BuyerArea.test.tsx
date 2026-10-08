import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BuyerArea } from './BuyerArea'

function setup() { render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><BuyerArea /></QueryClientProvider>) }

describe('BuyerArea', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input)
    if (path.endsWith('/me')) return new Response('', { status: 401 })
    if (path.endsWith('/request-code')) return new Response(JSON.stringify({ message: 'ok' }), { status: 202 })
    return new Response('', { status: 404 })
  })))

  it('requests a code without revealing whether the email has purchases', async () => {
    setup(); expect(await screen.findByRole('heading', { name: 'Acesse suas compras.' })).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: 'buyer@example.test' } })
    fireEvent.click(screen.getByRole('button', { name: /Enviar código/ }))
    expect(await screen.findByRole('heading', { name: 'Confira seu e-mail.' })).toBeInTheDocument()
    expect(screen.getByText('buyer@example.test')).toBeInTheDocument()
  })

  it('renders an authenticated empty library', async () => {
    vi.mocked(fetch).mockImplementation(async (input) => String(input).endsWith('/me') ? new Response(JSON.stringify({ email: 'buyer@example.test' })) : new Response(JSON.stringify([])))
    setup(); expect(await screen.findByRole('heading', { name: 'Minhas compras' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Nenhuma compra disponível' })).toBeInTheDocument())
  })
})
