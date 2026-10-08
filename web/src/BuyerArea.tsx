import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, ArrowUpRight, BookOpen, LoaderCircle, LogOut, Mail, ShieldCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { buyerClient, type BuyerProfile } from './lib/buyer-client'
import { ApiError } from './lib/auth-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'

const emailSchema = z.object({ email: z.string().trim().email('Informe um e-mail válido.').max(254) })
const codeSchema = z.object({ code: z.string().regex(/^\d{6}$/, 'Informe os seis números do código.') })

function BuyerLogin({ authenticated }: { authenticated: (profile: BuyerProfile) => void }) {
  const [email, setEmail] = useState('')
  const emailForm = useForm({ resolver: zodResolver(emailSchema), defaultValues: { email: '' } })
  const codeForm = useForm({ resolver: zodResolver(codeSchema), defaultValues: { code: '' } })
  const request = useMutation({ mutationFn: (value: string) => buyerClient.requestCode(value), onSuccess: (_, value) => setEmail(value) })
  const verify = useMutation({ mutationFn: (code: string) => buyerClient.verifyCode(email, code), onSuccess: authenticated })
  const error = request.error ?? verify.error
  return <div className="buyer-auth-shell"><a className="brand" href="/"><span className="brand-symbol"><ArrowUpRight /></span>zyven<span className="brand-dot">.</span></a><section className="buyer-login-card">
    <div className="buyer-login-icon">{email ? <ShieldCheck /> : <Mail />}</div><p className="eyebrow">Área do comprador</p><h1>{email ? 'Confira seu e-mail.' : 'Acesse suas compras.'}</h1>
    <p>{email ? <>Enviamos um código de seis números para <strong>{email}</strong>.</> : 'Use o mesmo e-mail informado no checkout para reunir seus acessos.'}</p>
    {!email ? <form onSubmit={emailForm.handleSubmit(value => request.mutate(value.email))}><div className="field"><Label htmlFor="buyer-email">E-mail</Label><Input id="buyer-email" type="email" autoComplete="email" placeholder="voce@exemplo.com" {...emailForm.register('email')} />{emailForm.formState.errors.email && <p className="field-error">{emailForm.formState.errors.email.message}</p>}</div><Button className="submit-button" disabled={request.isPending}>{request.isPending ? <LoaderCircle className="spin" /> : <>Enviar código <ArrowRight /></>}</Button></form>
      : <form onSubmit={codeForm.handleSubmit(value => verify.mutate(value.code))}><div className="field"><Label htmlFor="buyer-code">Código de acesso</Label><Input id="buyer-code" className="buyer-code-input" inputMode="numeric" autoComplete="one-time-code" maxLength={6} {...codeForm.register('code')} />{codeForm.formState.errors.code && <p className="field-error">{codeForm.formState.errors.code.message}</p>}</div><Button className="submit-button" disabled={verify.isPending}>{verify.isPending ? <LoaderCircle className="spin" /> : <>Ver minhas compras <ArrowRight /></>}</Button><Button variant="link" type="button" onClick={() => { setEmail(''); verify.reset(); codeForm.reset() }}>Usar outro e-mail</Button></form>}
    {error && <p role="alert" className="error-notice">{error instanceof ApiError ? error.message : 'Não foi possível conectar.'}</p>}
    <div className="security-note"><ShieldCheck size={16} /> Código temporário e de uso único.</div>
  </section></div>
}

function Library({ profile, logout }: { profile: BuyerProfile; logout: () => void }) {
  const purchases = useQuery({ queryKey: ['buyer-purchases'], queryFn: buyerClient.purchases })
  return <div className="buyer-library"><header><a className="brand" href="/"><span className="brand-symbol"><ArrowUpRight /></span>zyven<span className="brand-dot">.</span></a><div><span>{profile.email}</span><Button variant="ghost" onClick={logout}><LogOut /> Sair</Button></div></header><main><p className="eyebrow">Sua biblioteca</p><h1>Minhas compras</h1><p className="buyer-library-subtitle">Seus acessos ativos, reunidos em um só lugar.</p>
    {purchases.isPending ? <p role="status" className="loading"><LoaderCircle className="spin" /> Carregando compras...</p> : purchases.error ? <p role="alert" className="error-notice">Não foi possível carregar suas compras.</p> : !purchases.data?.length ? <section className="buyer-empty"><BookOpen /><h2>Nenhuma compra disponível</h2><p>Compras confirmadas feitas com este e-mail aparecerão aqui.</p></section> : <div className="buyer-purchase-grid">{purchases.data.map(item => <article className="buyer-purchase-card" key={item.id}><div className="buyer-cover"><BookOpen /></div><div><span className="buyer-access-status">Acesso ativo</span><h2>{item.offerName}</h2><p>{item.sellerName}</p><small>Compra confirmada em {new Date(item.paidAt).toLocaleDateString('pt-BR')}</small></div><PurchaseAction id={item.id} /></article>)}</div>}
  </main></div>
}

function PurchaseAction({ id }: { id: string }) {
  const detail = useQuery({ queryKey: ['buyer-purchase', id], queryFn: () => buyerClient.detail(id) })
  const item = detail.data?.items[0]
  return <Button asChild={!!item} disabled={!item}>{item ? <a href={item.url} target="_blank" rel="noopener noreferrer">{item.name}<ArrowUpRight /></a> : <span>{detail.isPending ? 'Carregando...' : 'Entrega indisponível'}</span>}</Button>
}

export function BuyerArea() {
  const client = useQueryClient()
  const session = useQuery({ queryKey: ['buyer-session'], queryFn: async () => { try { return await buyerClient.me() } catch (error) { if (error instanceof ApiError && error.status === 401) return null; throw error } }, retry: false })
  if (session.isPending) return <p role="status" className="loading"><LoaderCircle className="spin" /> Verificando acesso...</p>
  if (!session.data) return <BuyerLogin authenticated={profile => client.setQueryData(['buyer-session'], profile)} />
  return <Library profile={session.data} logout={() => void buyerClient.logout().finally(() => { client.setQueryData(['buyer-session'], null); client.removeQueries({ queryKey: ['buyer-purchases'] }) })} />
}
