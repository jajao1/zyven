import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Users } from 'lucide-react'
import { customerClient } from './lib/customer-client'
import { Button } from './components/ui/button'

export function Customers({ org, userId }: { org: string; userId: string }) {
  const [page, setPage] = useState(1)
  const query = useQuery({ queryKey: ['organizations', userId, org, 'customers', page], queryFn: () => customerClient.list(org, page), retry: false })
  return <section className="customers-page" aria-labelledby="customers-title">
    <div className="overview-heading"><div><p className="eyebrow">Relacionamento</p><h1 id="customers-title">Clientes</h1><p>Contatos identificados nos checkouts desta organização.</p></div><span className="overview-folio">04</span></div>
    {query.isPending ? <p role="status">Carregando clientes...</p> : query.isError ? <div className="error-notice"><p role="alert">Não foi possível carregar os clientes.</p><Button variant="outline" onClick={() => void query.refetch()}>Tentar novamente</Button></div> : !query.data.total ? <div className="empty-state"><Users /><h2>Nenhum cliente ainda</h2><p>Os clientes aparecerão aqui quando iniciarem um checkout publicado.</p></div> : <>
      <div className="directory-summary"><strong>{query.data.total}</strong><span>{query.data.total === 1 ? 'cliente identificado' : 'clientes identificados'}</span></div>
      <div className="customer-table" role="table" aria-label="Clientes"><div className="customer-head" role="row"><span>Cliente</span><span>Contato</span><span>Cadastro</span></div>{query.data.items.map(customer => <div className="customer-row" role="row" key={customer.id}><div><strong>{customer.name}</strong><span>{customer.email}</span></div><span>{customer.phone || 'Não informado'}</span><time dateTime={customer.createdAt}>{new Intl.DateTimeFormat('pt-BR').format(new Date(customer.createdAt))}</time></div>)}</div>
      {query.data.total > query.data.pageSize && <div className="pagination"><Button variant="outline" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page} de {Math.ceil(query.data.total / query.data.pageSize)}</span><Button variant="outline" disabled={page * query.data.pageSize >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</Button></div>}
    </>}
  </section>
}
