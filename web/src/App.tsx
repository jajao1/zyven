import { Organizations } from './Organizations'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowUpRight, ArrowRight, LoaderCircle, LogOut, ShieldCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ApiError, authClient, type User } from './lib/auth-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'

const fields = {
  email: z.string().email('Informe um e-mail válido.').max(254),
  password: z.string().min(1, 'Informe sua senha.').max(128),
  displayName: z.string(),
}
const loginSchema = z.object(fields)
const registerSchema = z.object({ ...fields,
  displayName: z.string().trim().min(2, 'Informe seu nome.').max(100, 'Use até 100 caracteres.'),
  password: z.string().min(12).max(128),
})
type FormValues = z.infer<typeof loginSchema>

function AuthForm({ onAuthenticated }: { onAuthenticated: (user: User) => Promise<void> }) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [showPassword, setShowPassword] = useState(false)
  const registerMode = mode === 'register'
  const form = useForm<FormValues>({ resolver: zodResolver(registerMode ? registerSchema : loginSchema),
    defaultValues: { email: '', password: '', displayName: '' } })
  const mutation = useMutation({
    mutationFn: (data: FormValues) => registerMode ? authClient.register(data) : authClient.login(data),
    onSuccess: async (session) => { form.reset(); await onAuthenticated(session.user) },
  })
  function switchMode() { setMode(registerMode ? 'login' : 'register'); form.reset(); mutation.reset() }
  const errors = form.formState.errors
  return <section className="auth-panel" aria-label={registerMode ? 'Cadastro' : 'Login'}>
    <div className="auth-heading">
      <p className="eyebrow">{registerMode ? 'Seu próximo começo' : 'Bom ter você de volta'}</p>
      <h1>{registerMode ? 'Crie sua conta.' : 'Entre na sua conta.'}</h1>
      <p>{registerMode ? 'Comece com seu nome, e-mail e uma senha segura.' : 'Acesse seu espaço na Zyven.'}</p>
    </div>
    <form noValidate onSubmit={form.handleSubmit(data => mutation.mutate(data))}>
      {registerMode && <div className="field">
        <Label htmlFor="displayName">Seu nome</Label>
        <Input id="displayName" autoComplete="name" maxLength={100} {...form.register('displayName')} aria-invalid={!!errors.displayName} aria-describedby={errors.displayName ? 'name-error' : undefined} />
        {errors.displayName && <p className="field-error" id="name-error">{errors.displayName.message}</p>}
      </div>}
      <div className="field">
        <Label htmlFor="email">E-mail</Label>
        <Input id="email" type="email" autoComplete="email" placeholder="voce@exemplo.com" maxLength={254} {...form.register('email')} aria-invalid={!!errors.email} aria-describedby={errors.email ? 'email-error' : undefined} />
        {errors.email && <p className="field-error" id="email-error">{errors.email.message}</p>}
      </div>
      <div className="field">
        <div className="field-label"><Label htmlFor="password">Senha</Label><button className="text-control" type="button" onClick={() => setShowPassword(!showPassword)} aria-pressed={showPassword}>{showPassword ? 'Ocultar' : 'Mostrar'}</button></div>
        <Input id="password" type={showPassword ? 'text' : 'password'} autoComplete={registerMode ? 'new-password' : 'current-password'} maxLength={128} {...form.register('password')} aria-invalid={!!errors.password} aria-describedby="password-help" />
        <p id="password-help" className={errors.password ? 'field-error' : 'field-help'}>
          {registerMode ? 'Use de 12 a 128 caracteres. Uma frase longa é uma boa escolha.' : errors.password?.message}
        </p>
      </div>
      {mutation.error && <p className="error-notice" role="alert">{mutation.error instanceof ApiError ? mutation.error.message : 'Não foi possível conectar. Verifique sua conexão e tente novamente.'}</p>}
      <Button className="submit-button" type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? <><LoaderCircle className="spin" /> Aguarde...</> : <>{registerMode ? 'Criar conta' : 'Entrar'}<ArrowRight /></>}
      </Button>
    </form>
    <div className="switch-mode"><span>{registerMode ? 'Já tem uma conta?' : 'Ainda não tem conta?'}</span><Button variant="link" type="button" onClick={switchMode} disabled={mutation.isPending}>{registerMode ? 'Entrar na conta' : 'Criar uma conta'}</Button></div>
    <div className="security-note"><ShieldCheck size={16} /><span>Seu acesso, protegido.</span></div>
  </section>
}

function Account({ user, onLogout }: { user: User; onLogout: () => Promise<void> }) {
  const logout = useMutation({ mutationFn: () => authClient.logout(), onSuccess: onLogout })
  return <div className="account-menu"><h1 className="sr-only">Olá, {user.displayName}.</h1><span><strong>{user.displayName}</strong><small>{user.email}</small></span><Button variant="ghost" onClick={() => logout.mutate()} disabled={logout.isPending}><LogOut />{logout.isPending ? 'Saindo...' : 'Sair da conta'}</Button>{logout.error && <span role="alert" className="sr-only">Não foi possível encerrar a sessão.</span>}</div>
}

export default function App() {
  const client = useQueryClient()
  const session = useQuery<User | null>({
    queryKey: ['session'],
    queryFn: async () => {
      try { return authClient.token() ? await authClient.me() : (await authClient.refresh()).user }
      catch (error) { if (error instanceof ApiError && error.status === 401) return null; throw error }
    },
    retry: false,
    staleTime: 60_000,
    refetchOnWindowFocus: true,
  })
  const clearSession = async () => { await client.cancelQueries({ queryKey: ['session'] }); client.setQueryData(['session'], null); client.removeQueries({ predicate: query => query.queryKey[0] !== 'session' }) }
  return <div className="app-shell">
    <header className="topbar"><a href="/" className="brand" aria-label="Zyven, início"><span className="brand-symbol"><ArrowUpRight /></span>zyven<span className="brand-dot">.</span></a>{session.data ? <Account user={session.data} onLogout={clearSession} /> : <span className="header-note">Seu conhecimento. Novas possibilidades.</span>}</header>
    <main className={session.data ? "main-grid authenticated-grid" : "main-grid"}>
      {!session.data && <aside className="intro-panel">
        <div><p className="eyebrow">Para quem tem algo a compartilhar</p><h2>Sua próxima<br />ideia começa<br /><span>aqui.</span></h2><p className="intro-copy">Um espaço para transformar o que você sabe em algo que as pessoas querem descobrir.</p></div>
        <div className="intro-footer"><span>Crie. Compartilhe. Conecte.</span><ArrowUpRight size={28} /></div>
      </aside>}
      <div className="form-column">
        {session.isPending ? <p role="status" className="loading"><LoaderCircle className="spin" /> Verificando sua sessão...</p>
          : session.isError ? <div className="auth-panel"><h1>Vamos tentar de novo?</h1><p role="alert">Não foi possível conectar à Zyven.</p><Button onClick={() => void session.refetch()}>Tentar novamente</Button></div>
          : session.data ? <Organizations key={session.data.id} userId={session.data.id} />
          : <AuthForm onAuthenticated={async user => {
            await client.cancelQueries({ queryKey: ['session'] })
            client.removeQueries({ predicate: query => query.queryKey[0] !== 'session' })
            client.setQueryData(['session'], user)
          }} />}
      </div>
    </main>
    <footer className="page-footer"><span>Zyven</span><span>Feito para suas próximas ideias.</span></footer>
  </div>
}
