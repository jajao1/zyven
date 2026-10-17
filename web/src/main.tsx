import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { PublicOfferPage } from './PublicOfferPage'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BuyerArea } from './BuyerArea'

const queryClient = new QueryClient()
const publicSlug = /^\/o\/([^/]+)\/?$/.exec(window.location.pathname)?.[1]
const buyerArea = window.location.pathname.startsWith('/buyer')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>{publicSlug ? <PublicOfferPage slug={publicSlug} /> : buyerArea ? <BuyerArea /> : <App />}</QueryClientProvider>
  </StrictMode>,
)
