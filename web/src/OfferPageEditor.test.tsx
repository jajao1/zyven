import { render, screen, fireEvent, waitFor, cleanup, act } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { OfferPageEditor } from './OfferPageEditor'
import { pageClient, type PageContent } from './lib/page-client'
vi.mock('./lib/page-client', () => ({ pageClient: { get: vi.fn(), save: vi.fn(), externalLink: vi.fn(), saveExternalLink: vi.fn(), automation: vi.fn(), saveAutomation: vi.fn(), publishAutomation: vi.fn() } }))
afterEach(() => { cleanup(); vi.clearAllMocks() })
it('reopens with a fresh saved baseline while preserving an open draft', async () => {
  const initial: PageContent = { title: 'Original', subtitle: '', description: '', benefits: [], testimonials: [], faq: [], guarantee: '', cta: 'Continue', fields: [], color: '#002fa7' }
  vi.mocked(pageClient.get).mockResolvedValueOnce(initial)
  vi.mocked(pageClient.save).mockResolvedValue({ ...initial, title: 'New title' })
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><OfferPageEditor org="org" userId="user" offer="offer" slug="slug" /></QueryClientProvider>)
  fireEvent.click(screen.getByRole('button', { name: 'Editar página pública' }))
  fireEvent.change(await screen.findByLabelText('Título da página'), { target: { value: 'New title' } }); fireEvent.click(screen.getByRole('button', { name: 'Salvar página' })); await screen.findByText('Página salva.')
  fireEvent.click(screen.getByRole('button', { name: 'Fechar editor da página' }))
  let release!: (value: PageContent) => void
  vi.mocked(pageClient.get).mockReturnValueOnce(new Promise(resolve => { release = resolve }))
  fireEvent.click(screen.getByRole('button', { name: 'Editar página pública' }))
  await waitFor(() => expect(pageClient.get).toHaveBeenCalledTimes(2))
  await act(async () => release({ ...initial, title: 'New title' }))
  expect(await screen.findByLabelText('Título da página')).toHaveValue('New title')
})
it('configures an HTTPS external-link delivery', async () => {
  vi.mocked(pageClient.externalLink).mockRejectedValue(new (await import('./lib/auth-client')).ApiError(404, 'Entrega não encontrada.'))
  vi.mocked(pageClient.saveExternalLink).mockResolvedValue({ id: 'delivery', type: 'EXTERNAL_LINK', name: 'Acessar curso', url: 'https://members.example.test/course', status: 'ACTIVE' })
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><OfferPageEditor org="org" userId="user" offer="offer" slug="slug" /></QueryClientProvider>)
  fireEvent.click(screen.getByRole('button', { name: 'Configurar entrega' }))
  fireEvent.change(await screen.findByLabelText('Nome do acesso'), { target: { value: 'Acessar curso' } }); fireEvent.change(screen.getByLabelText('Link HTTPS'), { target: { value: 'https://members.example.test/course' } })
  fireEvent.click(screen.getByRole('button', { name: 'Salvar entrega' })); await screen.findByText('Entrega salva.')
  expect(pageClient.saveExternalLink).toHaveBeenCalledWith('org', 'offer', { name: 'Acessar curso', url: 'https://members.example.test/course' })
})
it('builds, reorders and publishes a message funnel', async () => {
  vi.mocked(pageClient.automation).mockResolvedValue({ id: '', publishedVersion: 0, steps: [] })
  vi.mocked(pageClient.saveAutomation).mockResolvedValue({ id: 'automation', publishedVersion: 0, steps: [] })
  vi.mocked(pageClient.publishAutomation).mockResolvedValue({ id: 'automation', publishedVersion: 1, publishedAt: new Date().toISOString(), steps: [] })
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><OfferPageEditor org="org" userId="user" offer="offer" slug="slug" /></QueryClientProvider>)
  fireEvent.click(screen.getByRole('button', { name: 'Editar funil de mensagens' }))
  expect(await screen.findByDisplayValue('Olá, {{buyer_name}}! Sua compra foi confirmada.')).toBeInTheDocument()
  fireEvent.change(screen.getByDisplayValue('Olá, {{buyer_name}}! Sua compra foi confirmada.'), { target: { value: 'Bem-vindo, {{buyer_name}}' } })
  fireEvent.click(screen.getByRole('button', { name: 'Publicar funil' })); await screen.findByText('Funil publicado com sucesso.')
  expect(pageClient.saveAutomation).toHaveBeenCalledWith('org', 'offer', expect.arrayContaining([expect.objectContaining({ text: 'Bem-vindo, {{buyer_name}}' })]))
  expect(pageClient.publishAutomation).toHaveBeenCalledWith('org', 'offer')
})
