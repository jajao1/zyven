import { OfferPageEditor } from './OfferPageEditor'
import { useState, useId, useRef, useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { productSchema, offerSchema } from './lib/catalog-validation'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'
import { ApiError } from './lib/auth-client'
import { catalogClient as api, statusNames, type Product, type Offer, type ProductInput, type OfferInput } from './lib/catalog-client'
import { navigateCatalog, useCatalogLocation } from './lib/catalog-navigation'
import type { Role } from './lib/organization-client'
import { Box, CircleCheck, Plus, Search, ShoppingBag } from 'lucide-react'
function ErrorNotice({ error }: { error: Error | null }) { return error ? <p role="alert" className="error-notice">{error instanceof ApiError ? error.message : 'Não foi possível conectar. Tente novamente.'}</p> : null }
function StatusOptions() { return Object.entries(statusNames).map(([key, label]) => <option key={key} value={key}>{label}</option>) }
type EditorDone = (isCurrent: () => boolean) => Promise<void>
function useEditorCompletion(done: EditorDone) {
  const mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  return () => done(() => mounted.current)
}
function ProductEditor({ org, item, done }: { org: string; item?: Product; done: EditorDone }) {
  const form = useForm<ProductInput>({ resolver: zodResolver(productSchema), defaultValues: item ? { ...item, imageUrl: item.imageUrl ?? '' } : { name: '', slug: '', description: '', imageUrl: '', status: 'DRAFT' } })
  const mutation = useMutation({ mutationFn: (data: ProductInput) => api.saveProduct(org, data, item?.id), onSuccess: useEditorCompletion(done) })
  return <form className="catalog-form" noValidate onSubmit={form.handleSubmit(data => mutation.mutate(data))}><h3>{item ? 'Editar produto' : 'Novo produto'}</h3>
    <div className="field"><Label htmlFor="product-name">Nome do produto</Label><Input id="product-name" maxLength={200} {...form.register('name')} /></div>
    <div className="field"><Label htmlFor="product-slug">Slug do produto</Label><Input id="product-slug" maxLength={100} {...form.register('slug')} /><p className="field-help">Identificador único nesta organização. Ex.: comunidade-premium</p></div>
    <div className="field"><Label htmlFor="product-description">Descrição do produto</Label><textarea id="product-description" maxLength={10000} {...form.register('description')} /></div>
    <div className="field"><Label htmlFor="product-image">URL da imagem</Label><Input id="product-image" type="url" maxLength={2048} {...form.register('imageUrl')} /></div>
    {item ? <div className="field"><Label htmlFor="product-status">Status do produto</Label><select id="product-status" {...form.register('status')}><StatusOptions /></select></div> : <p className="field-help">O produto será criado como rascunho.</p>}
    {Object.entries(form.formState.errors).map(([key, error]) => <p key={key} role="alert" className="field-error">{error.message}</p>)}<ErrorNotice error={mutation.error} />
    <div className="catalog-actions"><Button disabled={mutation.isPending}>{item ? 'Salvar produto' : 'Criar produto'}</Button><Button type="button" variant="outline" onClick={() => navigateCatalog('/products', org)}>Cancelar</Button></div>
  </form>
}
function ProductPicker({ org, userId, value, onChange }: { org: string; userId: string; value: string; onChange: (id: string) => void }) {
  const [page, setPage] = useState(1)
  const list = useQuery({ queryKey: ['organizations', userId, org, 'product-picker', page], queryFn: () => api.products(org, page) })
  const selected = useQuery({ queryKey: ['organizations', userId, org, 'products', value], queryFn: () => api.product(org, value), enabled: !!value })
  return <div className="field"><Label htmlFor="offer-product">Produto da oferta</Label><select id="offer-product" value={value} onChange={event => onChange(event.target.value)}><option value="">Selecione um produto</option>{selected.data && !list.data?.items.some(x => x.id === value) && <option value={value}>{selected.data.name}</option>}{list.data?.items.map(product => <option key={product.id} value={product.id}>{product.name}</option>)}</select><ErrorNotice error={list.error ?? selected.error} />
    {list.data && list.data.total > 20 && <div className="pagination"><Button type="button" variant="outline" disabled={page === 1} onClick={() => setPage(page - 1)}>Produtos anteriores</Button><span>Página {page}</span><Button type="button" variant="outline" disabled={page * 20 >= list.data.total} onClick={() => setPage(page + 1)}>Próximos produtos</Button></div>}
  </div>
}
function OfferEditor({ org, userId, item, done }: { org: string; userId: string; item?: Offer; done: EditorDone }) {
  const form = useForm<OfferInput>({ resolver: zodResolver(offerSchema), defaultValues: item ?? { productId: '', name: '', slug: '', headline: '', description: '', price: '', currency: 'BRL', billingType: 'ONE_TIME', status: 'DRAFT' } })
  const productId = useWatch({ control: form.control, name: 'productId' })
  const mutation = useMutation({ mutationFn: (data: OfferInput) => api.saveOffer(org, data, item?.id), onSuccess: useEditorCompletion(done) })
  return <form className="catalog-form" noValidate onSubmit={form.handleSubmit(data => mutation.mutate(data))}><h3>{item ? 'Editar oferta' : 'Nova oferta'}</h3>
    <ProductPicker org={org} userId={userId} value={productId} onChange={id => form.setValue('productId', id, { shouldValidate: true })} />
    <div className="field"><Label htmlFor="offer-name">Nome da oferta</Label><Input id="offer-name" maxLength={200} {...form.register('name')} /></div>
    <div className="field"><Label htmlFor="offer-slug">Slug da oferta</Label><Input id="offer-slug" maxLength={100} {...form.register('slug')} /><p className="field-help">Identificador exclusivo da oferta. Será usado na página pública.</p></div>
    <div className="field"><Label htmlFor="offer-headline">Chamada</Label><Input id="offer-headline" maxLength={300} {...form.register('headline')} /></div>
    <div className="field"><Label htmlFor="offer-description">Descrição da oferta</Label><textarea id="offer-description" maxLength={10000} {...form.register('description')} /></div>
    <div className="catalog-columns"><div className="field"><Label htmlFor="offer-price">Preço</Label><Input id="offer-price" inputMode="decimal" placeholder="19.90" {...form.register('price')} /><p className="field-help">Use ponto para separar os centavos.</p></div><div className="field"><Label htmlFor="offer-currency">Moeda</Label><Input id="offer-currency" maxLength={3} {...form.register('currency')} /></div></div>
    <div className="field"><Label htmlFor="offer-billing">Tipo de cobrança</Label><select id="offer-billing" {...form.register('billingType')}><option value="ONE_TIME">Pagamento único</option><option value="SUBSCRIPTION">Assinatura</option></select></div>
    {item ? <div className="field"><Label htmlFor="offer-status">Status da oferta</Label><select id="offer-status" {...form.register('status')}><StatusOptions /></select></div> : <p className="field-help">A oferta será criada como rascunho.</p>}
    {Object.entries(form.formState.errors).map(([key, error]) => <p key={key} role="alert" className="field-error">{error.message}</p>)}<ErrorNotice error={mutation.error} />
    <div className="catalog-actions"><Button disabled={mutation.isPending}>{item ? 'Salvar oferta' : 'Criar oferta'}</Button><Button type="button" variant="outline" onClick={() => navigateCatalog('/offers', org)}>Cancelar</Button></div>
  </form>
}
function CatalogDetail({ org, userId, kind, id, canWrite, done }: { org: string; userId: string; kind: 'products' | 'offers'; id: string; canWrite: boolean; done: EditorDone }) {
  // A fresh editor instance must load its baseline before mounting the form.
  // Subsequent cache refreshes do not reset the user's draft.
  const editorId = useId()
  const product = useQuery({ queryKey: ['organizations', userId, org, 'products', id, 'editor', editorId], gcTime: 0, queryFn: () => api.product(org, id), enabled: kind === 'products' && id !== 'new', retry: false })
  const offer = useQuery({ queryKey: ['organizations', userId, org, 'offers', id, 'editor', editorId], gcTime: 0, queryFn: () => api.offer(org, id), enabled: kind === 'offers' && id !== 'new', retry: false })
  const query = kind === 'products' ? product : offer
  if (id !== 'new' && query.isPending) return <p role="status">Carregando item...</p>
  if (query.error) return <><ErrorNotice error={query.error} /><Button variant="outline" onClick={() => void query.refetch()}>Tentar novamente</Button></>
  if (!canWrite) return <div><p>Seu papel permite apenas consultar o catálogo.</p>{query.data && <><h3>{query.data.name}</h3><p>{query.data.description}</p><p>{statusNames[query.data.status]}</p>{offer.data && <p>{offer.data.currency} {offer.data.price}</p>}</>}</div>
  return kind === 'products' ? <ProductEditor org={org} item={product.data} done={done} /> : <><OfferEditor org={org} userId={userId} item={offer.data} done={done} />{offer.data && <OfferPageEditor org={org} userId={userId} offer={offer.data.id} slug={offer.data.slug} />}</>
}
export function Catalog({ org, userId, role }: { org: string; userId: string; role: Role }) {
  const location = useCatalogLocation()
  const kind = location.kind ?? 'products'
  const item = location.org === org ? location.item : undefined
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('ALL')
  const client = useQueryClient()
  const canWrite = ['OWNER', 'ADMIN', 'OPERATOR'].includes(role)
  const products = useQuery({ queryKey: ['organizations', userId, org, 'products', 'list', page], queryFn: () => api.products(org, page), enabled: kind === 'products' && !item })
  const offers = useQuery({ queryKey: ['organizations', userId, org, 'offers', 'list', page], queryFn: () => api.offers(org, page), enabled: kind === 'offers' && !item })
  const list = kind === 'products' ? products : offers
  const visibleItems = list.data?.items.filter(entity => (status === 'ALL' || entity.status === status) && (!search.trim() || entity.name.toLocaleLowerCase('pt-BR').includes(search.trim().toLocaleLowerCase('pt-BR')) || entity.slug.toLocaleLowerCase('pt-BR').includes(search.trim().toLocaleLowerCase('pt-BR')))) ?? []
  const activeOnPage = list.data?.items.filter(entity => entity.status === 'ACTIVE').length ?? 0
  async function done(isCurrent: () => boolean) { await client.invalidateQueries({ queryKey: ['organizations', userId, org] }); if (isCurrent()) navigateCatalog(`/${kind}`, org) }
  const noun = kind === 'products' ? 'produto' : 'oferta'
  return <section className="catalog catalog-management" aria-label={kind === 'products' ? 'Produtos' : 'Ofertas'}>
    <header className="catalog-heading"><div><p className="catalog-context">O que você vende</p><h1>{kind === 'products' ? 'Produtos' : 'Ofertas'}</h1><p>{kind === 'products' ? 'Organize o conteúdo ou serviço entregue ao comprador. Um produto pode ser vendido por diferentes ofertas.' : 'Configure preço, cobrança e checkout. Cada oferta vende um produto em condições específicas.'}</p></div>
      <nav className="catalog-tabs" aria-label="Seções de vendas"><Button variant={kind === 'products' ? 'default' : 'ghost'} onClick={() => { setPage(1); setSearch(''); setStatus('ALL'); navigateCatalog('/products', org) }}><Box />Produtos</Button><Button variant={kind === 'offers' ? 'default' : 'ghost'} onClick={() => { setPage(1); setSearch(''); setStatus('ALL'); navigateCatalog('/offers', org) }}><ShoppingBag />Ofertas</Button></nav>
    </header>
    {item ? <><Button variant="ghost" onClick={() => navigateCatalog(`/${kind}`, org)}>Voltar à lista</Button><CatalogDetail key={`${userId}-${org}-${kind}-${item}-${role}`} org={org} userId={userId} kind={kind} id={item} canWrite={canWrite} done={done} /></> : <>
      {list.isPending ? <p role="status">Carregando catálogo...</p> : list.error ? <><ErrorNotice error={list.error} /><Button variant="outline" onClick={() => void list.refetch()}>Tentar novamente</Button></> : <>
        <div className="catalog-metrics"><article><span>Total de {kind === 'products' ? 'produtos' : 'ofertas'}</span><strong>{list.data?.total ?? 0}</strong><small>{list.data?.total === 1 ? '1 item registrado' : `${list.data?.total ?? 0} itens registrados`}</small></article><article><span>Ativos nesta página</span><strong>{activeOnPage}</strong><small><CircleCheck />Prontos para uso</small></article><article><span>{kind === 'products' ? 'Como são vendidos' : 'O que configuram'}</span><strong className="catalog-concept">{kind === 'products' ? 'Ofertas' : 'Checkout'}</strong><small>{kind === 'products' ? 'Preço e checkout ficam nas ofertas' : 'Preço e regras de cobrança'}</small></article></div>
        <div className="catalog-toolbar"><div className="catalog-search"><Search /><Input aria-label={`Buscar ${kind === 'products' ? 'produtos' : 'ofertas'}`} placeholder={`Buscar ${kind === 'products' ? 'produtos' : 'ofertas'} por nome ou identificador`} value={search} onChange={event => setSearch(event.target.value)} /></div><select aria-label="Filtrar por status" value={status} onChange={event => setStatus(event.target.value)}><option value="ALL">Todos os status</option><StatusOptions /></select>{canWrite && <Button onClick={() => navigateCatalog(`/${kind}/new`, org)}><Plus />{kind === 'products' ? 'Novo produto' : 'Nova oferta'}</Button>}</div>
        {!list.data?.items.length && <p className="catalog-empty">{kind === 'products' ? 'Seu catálogo começa com um produto.' : 'Crie uma oferta para um produto do catálogo.'}</p>}
        {!!list.data?.items.length && !visibleItems.length && <p className="catalog-empty">Nenhum {noun} corresponde aos filtros.</p>}
        <div className="catalog-table" role="table" aria-label={kind === 'products' ? 'Produtos cadastrados' : 'Ofertas cadastradas'}>{visibleItems.map(entity => <article role="row" key={entity.id}><div className="catalog-entity"><span><Box /></span><div><strong>{entity.name}</strong><small>{'price' in entity ? `${entity.currency} ${entity.price} · ${entity.billingType === 'ONE_TIME' ? 'Pagamento único' : 'Assinatura'}` : entity.description || 'Sem descrição'}</small></div></div><code>{entity.slug}</code><span className={`catalog-status ${entity.status.toLocaleLowerCase()}`}><i />{statusNames[entity.status]}</span><Button variant="outline" onClick={() => navigateCatalog(`/${kind}/${entity.id}`, org)}>{canWrite ? 'Editar' : 'Consultar'}</Button></article>)}</div>
        {list.data && list.data.total > 20 && <div className="pagination"><Button variant="outline" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page}</span><Button variant="outline" disabled={page * 20 >= list.data.total} onClick={() => setPage(page + 1)}>Próxima</Button></div>}
      </>}
    </>}
  </section>
}
