import { useQuery } from '@tanstack/react-query'
import { ArrowRight, Check, Circle, CreditCard, Package, ShoppingBag, ShieldCheck, Users } from 'lucide-react'
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
  const productTotal = products.data?.total ?? 0
  const offerTotal = offers.data?.total ?? 0
  const customerTotal = customers.data?.total ?? 0
  const hasPublishedOffer = offers.data?.items.some(offer => offer.status === 'ACTIVE') ?? false
  const steps = [
    { label: 'Crie seu primeiro produto', detail: productTotal ? 'Produto disponível no catálogo.' : 'Adicione o produto que será vendido.', done: productTotal > 0, path: productTotal ? '/products' : '/products/new' },
    { label: 'Monte uma oferta', detail: offerTotal ? 'Oferta vinculada ao seu produto.' : 'Defina preço e condições da oferta.', done: offerTotal > 0, path: offerTotal ? '/offers' : '/offers/new' },
    { label: 'Revise e publique a oferta', detail: hasPublishedOffer ? 'Oferta publicada e pronta para venda.' : 'Publique a página e libere o checkout.', done: hasPublishedOffer, path: '/offers' },
  ]
  const completedSteps = steps.filter(step => step.done).length
  const metrics = [
    { label: 'Produtos', value: productTotal, detail: productTotal === 1 ? '1 item no catálogo' : `${productTotal} itens no catálogo`, icon: Package },
    { label: 'Ofertas', value: offerTotal, detail: hasPublishedOffer ? 'Oferta ativa' : offerTotal ? 'Aguardando publicação' : 'Nenhuma oferta criada', icon: ShoppingBag },
    { label: 'Clientes', value: customerTotal, detail: customerTotal === 1 ? '1 contato registrado' : `${customerTotal} contatos registrados`, icon: Users },
  ]

  return <section className="workspace-overview" aria-labelledby="overview-title">
    <div className="overview-hero"><div><div className="overview-kickers"><span><i />Visão geral</span><small>Prontidão da operação</small></div><h1 id="overview-title">{organizationName}</h1><p>Acompanhe a estrutura da sua operação e continue de onde parou.</p></div><div className="overview-stage"><span>Fase inicial</span><strong>01</strong></div></div>
    {pending ? <p className="workspace-loading" role="status">Carregando visão geral...</p> : failed ? <div className="error-notice"><p role="alert">Não foi possível carregar a visão geral.</p><Button variant="outline" onClick={retry}>Tentar novamente</Button></div> : <>
      <span className="sr-only" role="region" aria-label="Resumo da organização">Produtos{productTotal}Ofertas{offerTotal}Clientes{customerTotal}Receita—Disponível após pagamentos</span>
      <div className="metric-grid">
        {metrics.map(metric => { const Icon = metric.icon; return <article key={metric.label}><div><span>{metric.label}</span><i><Icon /></i></div><strong>{metric.value}</strong><small><b />{metric.detail}</small></article> })}
        <article className="metric-unavailable"><div><span>Receita</span><i><CreditCard /></i></div><strong>—</strong><small><b />Disponível após pagamentos</small></article>
      </div>
      <div className="overview-columns">
        <section className="setup-panel"><div className="setup-heading"><div><span><Check /></span><div><h2>Próximos passos</h2><p>Complete os requisitos para abrir suas vendas.</p></div></div><div className="setup-progress"><strong><span>{completedSteps}/3</span> concluídos</strong><span><i style={{ width: `${completedSteps / 3 * 100}%` }} /></span></div></div>
          <ol>{steps.map((step, index) => <li className={step.done ? 'complete' : 'pending'} key={step.label}><span className="step-index">0{index + 1}</span>{step.done ? <Check aria-label="Concluído" /> : <Circle aria-label="Pendente" />}<span><strong>{step.label}</strong><small>{step.detail}</small></span><Button variant={step.done ? 'ghost' : 'default'} onClick={() => navigateWorkspace(step.path, org)}>{step.done ? 'Revisar' : 'Continuar'}<ArrowRight /></Button></li>)}</ol>
          {customerTotal > 0 && <div className="overview-quick-action"><Button variant="outline" onClick={() => navigateWorkspace('/customers', org)}><Users />Ver clientes</Button><span>{customerTotal === 1 ? '1 contato disponível' : `${customerTotal} contatos disponíveis`}</span></div>}
        </section>
        <aside className="availability-panel"><div className="payment-card-heading"><span><CreditCard /></span><small><i />Pendente</small></div><p className="eyebrow">Pagamentos</p><h2>Ainda não configurado</h2><p>A criação de cobranças ficará disponível após conectar sua conta PushinPay.</p><div className="payment-status"><span>Provedor PIX <strong>Não vinculado</strong></span><span>Recebimentos <strong>Instantâneos</strong></span></div><Button onClick={() => navigateWorkspace('/settings', org)}>Configurar PushinPay<ArrowRight /></Button><small className="payment-security"><ShieldCheck />Token armazenado com criptografia</small></aside>
      </div>
    </>}
  </section>
}
