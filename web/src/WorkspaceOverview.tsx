import { useQuery } from '@tanstack/react-query'
import { ArrowRight, Check, Circle, CreditCard, Users } from 'lucide-react'
import { catalogClient } from './lib/catalog-client'
import { customerClient } from './lib/customer-client'
import { navigateWorkspace } from './lib/catalog-navigation'
import { Button } from './components/ui/button'

export function WorkspaceOverview({ org, userId, organizationName }: { org: string; userId: string; organizationName: string }) {
  const products = useQuery({ queryKey: ['organizations', userId, org, 'products', 'overview'], queryFn: () => catalogClient.products(org) })
  const offers = useQuery({ queryKey: ['organizations', userId, org, 'offers', 'overview'], queryFn: () => catalogClient.offers(org) })
  const customers = useQuery({ queryKey: ['organizations', userId, org, 'customers', 'overview'], queryFn: () => customerClient.list(org) })
  const pending = products.isPending || offers.isPending || customers.isPending
  const failed = products.isError || offers.isError || customers.isError
  const retry = () => void Promise.all([products.refetch(), offers.refetch(), customers.refetch()])
  const steps = [
    { label: 'Crie seu primeiro produto', done: (products.data?.total ?? 0) > 0, path: '/products/new' },
    { label: 'Monte uma oferta', done: (offers.data?.total ?? 0) > 0, path: '/offers/new' },
    { label: 'Revise e publique a oferta', done: false, path: '/offers' },
  ]
  return <section className="workspace-overview" aria-labelledby="overview-title">
    <div className="overview-heading"><div><p className="eyebrow">Visão geral</p><h1 id="overview-title">{organizationName}</h1><p>Acompanhe a estrutura da sua operação e continue de onde parou.</p></div><span className="overview-folio">01</span></div>
    {pending ? <p role="status">Carregando visão geral...</p> : failed ? <div className="error-notice"><p role="alert">Não foi possível carregar a visão geral.</p><Button variant="outline" onClick={retry}>Tentar novamente</Button></div> : <>
      <div className="metric-grid" role="region" aria-label="Resumo da organização">
        <article><span>Produtos</span><strong>{products.data?.total ?? 0}</strong></article>
        <article><span>Ofertas</span><strong>{offers.data?.total ?? 0}</strong></article>
        <article><span>Clientes</span><strong>{customers.data?.total ?? 0}</strong></article>
        <article className="metric-unavailable"><span>Receita</span><strong>—</strong><small>Disponível após pagamentos</small></article>
      </div>
      <div className="overview-columns">
        <section className="setup-panel"><div className="panel-title"><span>Próximos passos</span><strong>{steps.filter(step => step.done).length}/3</strong></div>
          <ol>{steps.map((step, index) => <li key={step.label}><span className="step-index">0{index + 1}</span>{step.done ? <Check aria-label="Concluído" /> : <Circle aria-label="Pendente" />}<span>{step.label}</span><Button variant="ghost" onClick={() => navigateWorkspace(step.path, org)}>{step.done ? 'Revisar' : 'Continuar'}<ArrowRight /></Button></li>)}</ol>
        </section>
        <aside className="availability-panel"><CreditCard /><p className="eyebrow">Pagamentos</p><h2>Ainda não configurado</h2><p>A criação de cobranças ficará disponível após a integração segura com o provedor PIX.</p></aside>
      </div>
      {(customers.data?.total ?? 0) > 0 && <Button variant="outline" onClick={() => navigateWorkspace('/customers', org)}><Users />Ver clientes</Button>}
    </>}
  </section>
}
