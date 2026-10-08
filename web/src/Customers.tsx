import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AtSign, CalendarDays, FileText, Phone, Search, ShoppingBag, Users } from 'lucide-react'
import { customerClient } from './lib/customer-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toLocaleUpperCase('pt-BR')
}

export function Customers({ org, userId }: { org: string; userId: string }) {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const query = useQuery({ queryKey: ['organizations', userId, org, 'customers', page], queryFn: () => customerClient.list(org, page), retry: false })
  const normalizedSearch = search.trim().toLocaleLowerCase('pt-BR')
  const visible = query.data?.items.filter(customer => !normalizedSearch || [customer.name, customer.email, customer.phone, customer.document].some(value => value?.toLocaleLowerCase('pt-BR').includes(normalizedSearch))) ?? []
  const withPhone = query.data?.items.filter(customer => customer.phone).length ?? 0
  const withDocument = query.data?.items.filter(customer => customer.document).length ?? 0

  return <section className="customers-page" aria-labelledby="customers-title">
    <header className="customers-heading"><div><p>Relacionamento</p><h1 id="customers-title">Clientes</h1><span>Contatos identificados nos checkouts desta organização.</span></div></header>
    {query.isPending ? <p role="status">Carregando clientes...</p> : query.isError ? <div className="error-notice"><p role="alert">Não foi possível carregar os clientes.</p><Button variant="outline" onClick={() => void query.refetch()}>Tentar novamente</Button></div> : !query.data.total ? <div className="empty-state"><Users /><h2>Nenhum cliente ainda</h2><p>Os clientes aparecerão aqui quando iniciarem um checkout publicado.</p></div> : <>
      <div className="customer-metrics">
        <article><div><span>Clientes identificados</span><i><Users /></i></div><strong>{query.data.total}</strong><small>Sincronizados pelos checkouts</small></article>
        <article><div><span>Com telefone</span><i><Phone /></i></div><strong>{withPhone}</strong><small>Nesta página</small></article>
        <article><div><span>Com documento</span><i><FileText /></i></div><strong>{withDocument}</strong><small>Nesta página</small></article>
        <article><div><span>Origem</span><i><ShoppingBag /></i></div><strong className="customer-origin">Checkout</strong><small>Cadastro automático</small></article>
      </div>
      <div className="customer-toolbar"><div><Search /><Input aria-label="Buscar clientes" placeholder="Buscar por nome, e-mail, telefone ou documento" value={search} onChange={event => setSearch(event.target.value)} /></div><span><strong>{query.data.total}</strong><span>{query.data.total === 1 ? 'cliente identificado' : 'clientes identificados'}</span></span></div>
      {!visible.length ? <div className="customer-no-results"><Search /><h2>Nenhum cliente encontrado</h2><p>Revise o termo usado na busca.</p></div> : <div className="customer-directory" role="table" aria-label="Clientes">
        <div className="customer-directory-head" role="row"><span>Cliente</span><span>Contato</span><span>Documento</span><span>Cadastro</span></div>
        {visible.map(customer => <article className="customer-directory-row" role="row" key={customer.id}>
          <div className="customer-profile"><i>{initials(customer.name)}</i><div><strong>{customer.name}</strong><span><AtSign />{customer.email}</span></div></div>
          <span className={customer.phone ? '' : 'muted'}><Phone />{customer.phone || 'Não informado'}</span>
          <span className={customer.document ? '' : 'muted'}><FileText />{customer.document || 'Não informado'}</span>
          <time dateTime={customer.createdAt}><CalendarDays />{new Intl.DateTimeFormat('pt-BR').format(new Date(customer.createdAt))}</time>
        </article>)}
      </div>}
      {query.data.total > query.data.pageSize && <div className="pagination"><Button variant="outline" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page} de {Math.ceil(query.data.total / query.data.pageSize)}</span><Button variant="outline" disabled={page * query.data.pageSize >= query.data.total} onClick={() => setPage(page + 1)}>Próxima</Button></div>}
    </>}
  </section>
}
