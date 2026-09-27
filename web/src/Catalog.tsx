import { useState } from 'react'
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
function ErrorNotice({ error }: { error: Error | null }) { return error ? <p role="alert" className="error-notice">{error instanceof ApiError ? error.message : 'Não foi possível conectar. Tente novamente.'}</p> : null }
function StatusOptions() { return Object.entries(statusNames).map(([key, label]) => <option key={key} value={key}>{label}</option>) }
function ProductEditor({ org, item, done }: { org: string; item?: Product; done: () => Promise<void> }) {
  const form = useForm<ProductInput>({ resolver: zodResolver(productSchema), defaultValues: item ? { ...item, imageUrl: item.imageUrl ?? '' } : { name: '', slug: '', description: '', imageUrl: '', status: 'DRAFT' } })
  const mutation = useMutation({ mutationFn: (data: ProductInput) => api.saveProduct(org, data, item?.id), onSuccess: done })
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
function OfferEditor({ org, userId, item, done }: { org: string; userId: string; item?: Offer; done: () => Promise<void> }) {
  const form = useForm<OfferInput>({ resolver: zodResolver(offerSchema), defaultValues: item ?? { productId: '', name: '', slug: '', headline: '', description: '', price: '', currency: 'BRL', billingType: 'ONE_TIME', status: 'DRAFT' } })
  const productId = useWatch({ control: form.control, name: 'productId' })
  const mutation = useMutation({ mutationFn: (data: OfferInput) => api.saveOffer(org, data, item?.id), onSuccess: done })
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
function CatalogDetail({ org, userId, kind, id, canWrite, done }: { org: string; userId: string; kind: 'products' | 'offers'; id: string; canWrite: boolean; done: () => Promise<void> }) {
  const product = useQuery({ queryKey: ['organizations', userId, org, 'products', id], queryFn: () => api.product(org, id), enabled: kind === 'products' && id !== 'new', retry: false })
  const offer = useQuery({ queryKey: ['organizations', userId, org, 'offers', id], queryFn: () => api.offer(org, id), enabled: kind === 'offers' && id !== 'new', retry: false })
  const query = kind === 'products' ? product : offer
  if (id !== 'new' && query.isPending) return <p role="status">Carregando item...</p>
  if (query.error) return <><ErrorNotice error={query.error} /><Button variant="outline" onClick={() => void query.refetch()}>Tentar novamente</Button></>
  if (!canWrite) return <div><p>Seu papel permite apenas consultar o catálogo.</p>{query.data && <><h3>{query.data.name}</h3><p>{query.data.description}</p><p>{statusNames[query.data.status]}</p>{offer.data && <p>{offer.data.currency} {offer.data.price}</p>}</>}</div>
  return kind === 'products' ? <ProductEditor org={org} item={product.data} done={done} /> : <OfferEditor org={org} userId={userId} item={offer.data} done={done} />
}
export function Catalog({ org, userId, role }: { org: string; userId: string; role: Role }) {
  const location = useCatalogLocation()
  const kind = location.kind ?? 'products'
  const item = location.org === org ? location.item : undefined
  const [page, setPage] = useState(1)
  const client = useQueryClient()
  const canWrite = ['OWNER', 'ADMIN', 'OPERATOR'].includes(role)
  const products = useQuery({ queryKey: ['organizations', userId, org, 'products', 'list', page], queryFn: () => api.products(org, page), enabled: kind === 'products' && !item })
  const offers = useQuery({ queryKey: ['organizations', userId, org, 'offers', 'list', page], queryFn: () => api.offers(org, page), enabled: kind === 'offers' && !item })
  const list = kind === 'products' ? products : offers
  async function done() { await client.invalidateQueries({ queryKey: ['organizations', userId, org] }); navigateCatalog(`/${kind}`, org) }
  return <section className="catalog" aria-label="Catálogo"><div className="section-heading"><div><p className="eyebrow">O que você vende</p><h2>Catálogo</h2></div></div>
    <nav className="catalog-tabs" aria-label="Catálogo"><Button variant={kind === 'products' ? 'default' : 'outline'} onClick={() => { setPage(1); navigateCatalog('/products', org) }}>Produtos</Button><Button variant={kind === 'offers' ? 'default' : 'outline'} onClick={() => { setPage(1); navigateCatalog('/offers', org) }}>Ofertas</Button></nav>
    <p className="section-copy">{kind === 'products' ? 'Organize seus produtos. Cada produto pode ter várias ofertas.' : 'Defina o preço e as condições de cada oferta.'}</p>
    {item ? <><Button variant="ghost" onClick={() => navigateCatalog(`/${kind}`, org)}>Voltar à lista</Button><CatalogDetail key={`${kind}-${item}-${role}`} org={org} userId={userId} kind={kind} id={item} canWrite={canWrite} done={done} /></> : <>
      {canWrite && <Button onClick={() => navigateCatalog(`/${kind}/new`, org)}>{kind === 'products' ? 'Novo produto' : 'Nova oferta'}</Button>}
      {list.isPending ? <p role="status">Carregando catálogo...</p> : list.error ? <><ErrorNotice error={list.error} /><Button variant="outline" onClick={() => void list.refetch()}>Tentar novamente</Button></> : <>
        {!list.data?.items.length && <p className="catalog-empty">{kind === 'products' ? 'Seu catálogo começa com um produto.' : 'Crie uma oferta para um produto do catálogo.'}</p>}
        <ul className="catalog-list">{list.data?.items.map(entity => <li key={entity.id}><div><strong>{entity.name}</strong><span>{entity.slug}</span>{'price' in entity && <span>{entity.currency} {entity.price} · {entity.billingType === 'ONE_TIME' ? 'Pagamento único' : 'Assinatura'}</span>}</div><span className="role-badge">{statusNames[entity.status]}</span><Button variant="outline" onClick={() => navigateCatalog(`/${kind}/${entity.id}`, org)}>{canWrite ? 'Editar' : 'Consultar'}</Button></li>)}</ul>
        {list.data && list.data.total > 20 && <div className="pagination"><Button variant="outline" disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page}</span><Button variant="outline" disabled={page * 20 >= list.data.total} onClick={() => setPage(page + 1)}>Próxima</Button></div>}
      </>}
    </>}
  </section>
}
