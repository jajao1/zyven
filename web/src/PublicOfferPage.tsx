import { useState, useEffect } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import QRCode from 'qrcode'
import { pageClient, type BuyerInput, type Checkout, type PixPayment, type PublicOffer } from './lib/page-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'
const buyerSchema = z.object({ name: z.string().trim().min(2, 'Informe seu nome completo.').max(200), email: z.string().trim().email('Informe um email válido.').max(254), phone: z.string().max(30).refine(value => !value.trim() || /^\+[1-9][0-9]{6,14}$/.test(value.trim().replace(/[ ()-]/g, '')), 'Informe o telefone com + e código do país.'), document: z.string().regex(/^\D*(?:\d\D*){11}(?:(?:\d\D*){3})?$/, 'Informe um CPF ou CNPJ válido.').max(40), fields: z.record(z.string(), z.string().max(1000)) })
function CheckoutForm({ offer, complete }: { offer: PublicOffer; complete: (value: Checkout) => void }) {
  const form = useForm<BuyerInput>({ resolver: zodResolver(buyerSchema), defaultValues: { name: '', email: '', phone: '', document: '', fields: {} } })
  const mutation = useMutation({ mutationFn: (data: BuyerInput) => pageClient.create(offer.slug, data), onSuccess: complete })
  return <form className="catalog-form" onSubmit={form.handleSubmit(data => mutation.mutate(data))} aria-label="Seus dados"><p className="eyebrow">Checkout</p><h2>Seus dados</h2><p className="section-copy">Confira a oferta e informe seus dados para continuar.</p>
    <div className="field"><Label htmlFor="buyer-name">Nome completo</Label><Input id="buyer-name" autoComplete="name" maxLength={200} {...form.register('name')} /></div>
    <div className="field"><Label htmlFor="buyer-email">Email</Label><Input id="buyer-email" type="email" autoComplete="email" maxLength={254} {...form.register('email')} /></div>
    <div className="field"><Label htmlFor="buyer-phone">Telefone (opcional)</Label><Input id="buyer-phone" type="tel" autoComplete="tel" maxLength={30} aria-describedby="buyer-phone-help" {...form.register('phone')} /><p className="field-help" id="buyer-phone-help">Inclua + e o código do país. Exemplo: +55 11 99999-0000.</p></div>
    <div className="field"><Label htmlFor="buyer-document">CPF ou CNPJ</Label><Input id="buyer-document" inputMode="numeric" autoComplete="off" maxLength={40} {...form.register('document')} /></div>
    {offer.page.fields.map(field => <div className="field" key={field.key}><Label htmlFor={`buyer-custom-${field.key}`}>{field.label}{field.required ? ' *' : ''}</Label>{field.type === 'textarea' ? <textarea id={`buyer-custom-${field.key}`} maxLength={1000} required={field.required} {...form.register(`fields.${field.key}`)} /> : <Input id={`buyer-custom-${field.key}`} maxLength={1000} required={field.required} {...form.register(`fields.${field.key}`)} />}</div>)}
    {Object.entries(form.formState.errors).map(([key, error]) => <p className="field-error" role="alert" key={key}>{typeof error.message === 'string' ? error.message : 'Confira os campos adicionais.'}</p>)}
    {mutation.error && <p className="error-notice" role="alert">{mutation.error.message}</p>}
    <div className="checkout-total"><span>Total da oferta</span><strong>{offer.currency} {offer.price}</strong></div><p className="field-help">{offer.billingType === 'SUBSCRIPTION' ? 'Oferta de assinatura.' : 'Pagamento único.'} Você poderá gerar o PIX na próxima etapa.</p>
    <Button disabled={mutation.isPending}>{mutation.isPending ? 'Salvando...' : offer.page.cta}</Button>
  </form>
}
function PixCode({ value }: { value: string }) {
  const [src, setSrc] = useState('')
  useEffect(() => { let active = true; void QRCode.toString(value, { type: 'svg', width: 240, margin: 1 }).then(svg => { if (active) setSrc(`data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`) }); return () => { active = false } }, [value])
  return <>{src && <img className="pix-qr" src={src} alt="QR Code PIX" />}<code className="pix-code">{value}</code><Button type="button" variant="outline" onClick={() => void navigator.clipboard.writeText(value)}>Copiar código PIX</Button></>
}
function Receipt({ value }: { value: Checkout }) {
  const [payment, setPayment] = useState<PixPayment | null>(null)
  const mutation = useMutation({ mutationFn: () => pageClient.createPix(value.id), onSuccess: setPayment })
  useEffect(() => { if (!payment || !['PENDING', 'PROCESSING'].includes(payment.status)) return; const timer = window.setInterval(() => void pageClient.pix(value.id).then(setPayment).catch(() => undefined), 3000); return () => window.clearInterval(timer) }, [payment, value.id])
  const delivery = useQuery({ queryKey: ['delivery', value.id], queryFn: () => pageClient.delivery(value.id), enabled: value.status === 'COMPLETED' || payment?.status === 'PAID', retry: false })
  if (value.status === 'COMPLETED' || payment?.status === 'PAID') return <section className="checkout-receipt"><p className="eyebrow">Pagamento confirmado</p><h2>Sua compra está disponível</h2><p>{value.name}, o acesso foi liberado.</p>{delivery.isPending && <p role="status">Carregando sua entrega...</p>}{delivery.error && <p role="alert" className="error-notice">{delivery.error.message}</p>}{delivery.data?.items.map(item => <Button key={item.id} asChild><a href={item.url} target="_blank" rel="noopener noreferrer">{item.name}</a></Button>)}<Button variant="link" asChild><a href="/buyer/purchases">Ver todas as minhas compras</a></Button></section>
  return <section className="checkout-receipt"><p className="eyebrow">Pagamento PIX</p><h2>{payment ? 'Escaneie para pagar' : 'Finalize seu pedido'}</h2><p>{value.name}, pague {value.currency} {value.price} até {new Date(value.expiresAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}.</p><p className="field-help">A PushinPay atua exclusivamente como processadora de pagamentos e não possui responsabilidade pela entrega, suporte, conteúdo, qualidade ou cumprimento das obrigações relacionadas aos produtos ou serviços oferecidos pelo vendedor.</p>{payment?.pixCode ? <PixCode value={payment.pixCode} /> : <Button disabled={mutation.isPending} onClick={() => mutation.mutate()}>{mutation.isPending ? 'Gerando PIX...' : 'Gerar PIX'}</Button>}{mutation.error && <p role="alert" className="error-notice">{mutation.error.message}</p>}{payment && <p role="status">Aguardando confirmação do pagamento.</p>}</section>
}
export function PublicOfferPage({ slug }: { slug: string }) {
  const [checkoutId, setCheckoutId] = useState(() => new URLSearchParams(window.location.search).get('checkout'))
  const [created, setCreated] = useState<Checkout | null>(null)
  const offer = useQuery({ queryKey: ['public-offer', slug], queryFn: () => pageClient.offer(slug), retry: false })
  const title = offer.data && !offer.error ? `${offer.data.page.title} — Zyven` : 'Oferta — Zyven'
  useEffect(() => { document.title = title }, [title])
  const checkout = useQuery({ queryKey: ['public-checkout', checkoutId], queryFn: () => pageClient.checkout(checkoutId!), enabled: !!checkoutId && !created, retry: false, gcTime: 0, staleTime: 0 })
  function complete(value: Checkout) { setCreated(value); setCheckoutId(value.id); window.history.replaceState(null, '', `${window.location.pathname}?checkout=${encodeURIComponent(value.id)}`) }
  function restart() { setCreated(null); setCheckoutId(null); window.history.replaceState(null, '', window.location.pathname) }
  if (offer.isPending) return <main className="public-shell"><p role="status">Carregando oferta...</p></main>
  if (offer.error || !offer.data) return <main className="public-shell"><a href="/">Zyven</a><h1>Oferta indisponível</h1><p role="alert">{offer.error?.message ?? 'Esta oferta não está disponível.'}</p></main>
  const { page } = offer.data
  return <main className="public-shell" style={{ '--offer-color': page.color } as React.CSSProperties}><header className="public-header">{page.logoUrl ? <img src={page.logoUrl} alt={offer.data.productName} referrerPolicy="no-referrer" /> : <span>{offer.data.productName}</span>}<span>Oferta</span></header>
    <div className="public-grid"><article className="public-content"><p className="eyebrow">{offer.data.name}</p><h1>{page.title}</h1>{page.subtitle && <p className="public-subtitle">{page.subtitle}</p>}{page.imageUrl && <img className="public-media" src={page.imageUrl} alt={page.title} referrerPolicy="no-referrer" />}{page.videoUrl && <video className="public-media" controls preload="none" src={page.videoUrl} />}{page.description && <p className="public-description">{page.description}</p>}
      {page.benefits.length > 0 && <section><h2>O que está incluído</h2><ul>{page.benefits.map((benefit, i) => <li key={i}>{benefit}</li>)}</ul></section>}
      {page.testimonials.length > 0 && <section><h2>Depoimentos</h2>{page.testimonials.map((item, i) => <blockquote key={i}><p>{item.text}</p><cite>{item.name}</cite></blockquote>)}</section>}
      {page.faq.length > 0 && <section><h2>Perguntas frequentes</h2>{page.faq.map((item, i) => <details key={i}><summary>{item.question}</summary><p>{item.answer}</p></details>)}</section>}
      {page.guarantee && <section><h2>Garantia</h2><p>{page.guarantee}</p></section>}
    </article><aside className="public-checkout">{created ? <Receipt value={created} /> : checkoutId ? checkout.isPending ? <p role="status">Carregando checkout...</p> : checkout.error ? <><p role="alert">{checkout.error.message}</p><Button onClick={restart}>Começar novamente</Button></> : checkout.data && (checkout.data.offerSlug === slug ? <Receipt value={checkout.data} /> : <><p role="alert">Este checkout pertence a outra oferta.</p><Button onClick={restart}>Começar novamente</Button></>) : <CheckoutForm offer={offer.data} complete={complete} />}</aside></div><footer className="public-footer">Checkout por Zyven</footer></main>
}
