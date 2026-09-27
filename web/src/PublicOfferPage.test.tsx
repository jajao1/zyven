import { render, screen, fireEvent, waitFor, cleanup } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { PublicOfferPage } from './PublicOfferPage'
afterEach(() => { cleanup(); vi.unstubAllGlobals(); window.history.replaceState(null, '', '/') })
it('renders escaped content and submits buyer data without client price', async () => {
  const fetcher = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ slug: 'course', name: 'Course', productName: 'Course', price: '19.90', currency: 'BRL', billingType: 'ONE_TIME', page: { title: '<script>bad()</script>', subtitle: '', description: 'Lessons', benefits: [], testimonials: [], faq: [], guarantee: '', cta: 'Continuar', color: '#002fa7', fields: [{ key: 'company', label: 'Empresa', type: 'text', required: true }] } }))).mockResolvedValueOnce(new Response(JSON.stringify({ id: 'checkout-id', status: 'CREATED', price: '19.90', currency: 'BRL', expiresAt: '2099-01-01T00:00:00Z', name: 'Buyer', email: 'buyer@example.test', fields: {} }), { status: 201 }))
  vi.stubGlobal('fetch', fetcher)
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><PublicOfferPage slug="course" /></QueryClientProvider>)
  expect(await screen.findByText('<script>bad()</script>')).toBeInTheDocument(); expect(document.title).toBe('<script>bad()</script> — Zyven'); expect(document.querySelector('script')).toBeNull()
  fireEvent.change(screen.getByLabelText('Nome completo'), { target: { value: 'Buyer Name' } }); fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'buyer@example.test' } }); fireEvent.change(screen.getByLabelText('Empresa *'), { target: { value: 'Company' } })
  fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))
  expect(await screen.findByText('Dados recebidos')).toBeInTheDocument()
  await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2))
  const request = JSON.parse(fetcher.mock.calls[1][1].body); expect(request).not.toHaveProperty('price'); expect(request.fields.company).toBe('Company'); expect(fetcher.mock.calls[1][1].credentials).toBe('same-origin')
})
it('does not show a receipt belonging to another offer', async () => {
  window.history.replaceState(null, '', '/o/second?checkout=first-checkout')
  vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string) => Promise.resolve(new Response(JSON.stringify(url.includes('/offers/') ? { slug: 'second', name: 'Second', productName: 'Second product', price: '50.00', currency: 'BRL', billingType: 'ONE_TIME', page: { title: 'Second offer', subtitle: '', description: '', benefits: [], testimonials: [], faq: [], guarantee: '', cta: 'Continue', color: '#002fa7', fields: [] } } : { id: 'first-checkout', offerSlug: 'first', status: 'CREATED', price: '19.90', currency: 'BRL', expiresAt: '2099-01-01T00:00:00Z', name: 'Other buyer', email: 'other@example.test', fields: {} })))))
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><PublicOfferPage slug="second" /></QueryClientProvider>)
  expect(await screen.findByText('Este checkout pertence a outra oferta.')).toBeInTheDocument()
  expect(screen.queryByText('Dados recebidos')).not.toBeInTheDocument()
  expect(screen.queryByText(/Other buyer/)).not.toBeInTheDocument()
})

it('uses a neutral title while loading and when the offer is unavailable', async () => {
  document.title = 'Zyven — sua conta'
  let release!: (response: Response) => void
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise<Response>(resolve => { release = resolve })))
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><PublicOfferPage slug="unavailable" /></QueryClientProvider>)
  expect(document.title).toBe('Oferta — Zyven')
  release(new Response(JSON.stringify({ title: 'Oferta indisponível' }), { status: 404 }))
  await screen.findByRole('heading', { name: 'Oferta indisponível' })
  expect(document.title).toBe('Oferta — Zyven')
})
