import { Catalog } from './Catalog'
import { Customers } from './Customers'
import { WorkspaceOverview } from './WorkspaceOverview'
import { useCatalogLocation, navigateWorkspace, type WorkspaceView } from './lib/catalog-navigation'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Building2, Info, LayoutGrid, Package, Plus, Settings, ShoppingBag, Users } from 'lucide-react'
import { ApiError } from './lib/auth-client'
import { organizationClient as api, roleNames, type Role, type Member, type Page } from './lib/organization-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'

function ErrorNotice({ error }: { error: Error | null }) { return error ? <p role="alert" className="error-notice">{error instanceof ApiError ? error.message : 'Não foi possível conectar. Tente novamente.'}</p> : null }
function Pagination({ data, page, setPage }: { data?: Page<unknown>; page: number; setPage: (page: number) => void }) { if (!data || data.total <= data.pageSize) return null; return <div className="pagination"><Button variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page} de {Math.ceil(data.total / data.pageSize)}</span><Button variant="outline" disabled={page * data.pageSize >= data.total} onClick={() => setPage(page + 1)}>Próxima</Button></div> }
const roles = Object.keys(roleNames) as Role[]
function RoleOptions({ owner }: { owner: boolean }) { return roles.filter(role => owner || !['OWNER', 'ADMIN'].includes(role)).map(role => <option key={role} value={role}>{roleNames[role]}</option>) }
function MemberRow({ member, actor, pending, change, remove }: { member: Member; actor: Role; pending: boolean; change: (role: Role) => void; remove: () => void }) {
  const [role, setRole] = useState(member.role); const [confirm, setConfirm] = useState(false); const canManage = actor === 'OWNER' || actor === 'ADMIN' && !['OWNER', 'ADMIN'].includes(member.role)
  return <li className="member-row"><div className="member-identity"><strong>{member.displayName}</strong><span>{member.email}</span></div>{canManage ? <div className="member-actions"><select aria-label={`Papel de ${member.displayName}`} value={role} onChange={event => setRole(event.target.value as Role)} disabled={pending}><RoleOptions owner={actor === 'OWNER'} /></select><Button variant="outline" disabled={pending || role === member.role} onClick={() => change(role)}>Salvar papel</Button>{confirm ? <><span>Remover acesso?</span><Button variant="outline" disabled={pending} onClick={remove}>Confirmar remoção</Button><Button variant="ghost" disabled={pending} onClick={() => setConfirm(false)}>Cancelar</Button></> : <Button variant="ghost" disabled={pending} onClick={() => setConfirm(true)}>Remover</Button>}</div> : <span className="role-badge">{roleNames[member.role]}</span>}</li>
}

const destinations: Array<{ view: WorkspaceView; path: string; label: string; icon: typeof LayoutGrid }> = [
  { view: 'overview', path: '/dashboard', label: 'Visão geral', icon: LayoutGrid }, { view: 'products', path: '/products', label: 'Produtos', icon: Package }, { view: 'offers', path: '/offers', label: 'Ofertas', icon: ShoppingBag }, { view: 'customers', path: '/customers', label: 'Clientes', icon: Users }, { view: 'team', path: '/team', label: 'Equipe', icon: Users }, { view: 'settings', path: '/settings', label: 'Configurações', icon: Settings },
]

function Team({ id, userId, actor }: { id: string; userId: string; actor: Role }) {
  const client = useQueryClient(); const [page, setPage] = useState(1); const [email, setEmail] = useState(''); const [role, setRole] = useState<Role>('OPERATOR')
  const members = useQuery({ queryKey: ['organizations', userId, id, 'members', page], queryFn: () => api.members(id, page), retry: false }); const canManage = actor === 'OWNER' || actor === 'ADMIN'; const assignableRole = actor === 'ADMIN' && ['OWNER', 'ADMIN'].includes(role) ? 'OPERATOR' : role
  async function invalidate() { await client.invalidateQueries({ queryKey: ['organizations', userId, id, 'members'] }) }
  const add = useMutation({ mutationFn: () => api.add(id, email.trim(), assignableRole), onSuccess: async () => { setEmail(''); await invalidate() } }); const change = useMutation({ mutationFn: ({ memberId, role }: { memberId: string; role: Role }) => api.change(id, memberId, role), onSettled: invalidate }); const remove = useMutation({ mutationFn: (memberId: string) => api.remove(id, memberId), onSettled: invalidate })
  return <section><div className="overview-heading"><div><p className="eyebrow">Acessos</p><h1>Equipe</h1><p>Cada pessoa acessa esta organização de acordo com seu papel.</p></div><span className="overview-folio">05</span></div>{canManage && <form className="member-form" onSubmit={event => { event.preventDefault(); add.mutate() }}><div className="field"><Label htmlFor="member-email">E-mail da pessoa</Label><Input id="member-email" type="email" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} /><p className="field-help">A pessoa precisa ter uma conta na Zyven.</p></div><div className="field"><Label htmlFor="new-role">Papel</Label><select id="new-role" value={assignableRole} onChange={event => setRole(event.target.value as Role)}><RoleOptions owner={actor === 'OWNER'} /></select></div><Button disabled={add.isPending}><Plus />Adicionar membro</Button></form>}<ErrorNotice error={add.error ?? change.error ?? remove.error} />{members.isPending ? <p role="status">Carregando equipe...</p> : members.error ? <><ErrorNotice error={members.error} /><Button variant="outline" onClick={() => void members.refetch()}>Recarregar equipe</Button></> : <><ul className="member-list">{members.data?.items.map(member => <MemberRow key={`${member.id}-${member.role}`} member={member} actor={actor} pending={change.isPending || remove.isPending} change={role => change.mutate({ memberId: member.id, role })} remove={() => remove.mutate(member.id)} />)}</ul><Pagination data={members.data} page={page} setPage={setPage} /></>}</section>
}

function OrganizationSettings({ id, userId, organization }: { id: string; userId: string; organization: { name: string; role: Role } }) {
  const client = useQueryClient(); const [name, setName] = useState(organization.name); const [account, setAccount] = useState<{ status: string; provider: string; tokenFingerprint: string } | null>(null); const canManage = organization.role === 'OWNER' || organization.role === 'ADMIN'; const rename = useMutation({ mutationFn: () => api.rename(id, name.trim()), onSuccess: async () => { await client.invalidateQueries({ queryKey: ['organizations', userId] }) } })
  const tokenForm = useForm<{ token: string }>({ resolver: zodResolver(z.object({ token: z.string().trim().min(8, 'Informe um token válido.').max(4096) })), defaultValues: { token: '' } })
  const connect = useMutation({ mutationFn: ({ token }: { token: string }) => api.connectPayment(id, token.trim()), onSuccess: result => { setAccount(result); tokenForm.reset() } })
  return <section><div className="overview-heading"><div><p className="eyebrow">Organização</p><h1>Configurações</h1><p>Dados gerais e seu nível de acesso.</p></div><span className="overview-folio">06</span></div><div className="settings-grid"><form onSubmit={event => { event.preventDefault(); rename.mutate() }}><div className="field"><Label htmlFor="org-name">Nome da organização</Label><Input id="org-name" value={name} maxLength={100} disabled={!canManage} onChange={event => setName(event.target.value)} /></div>{canManage && <Button disabled={rename.isPending || !name.trim()}>Salvar alterações</Button>}</form><dl><dt>Seu papel</dt><dd>{roleNames[organization.role]}</dd></dl>{canManage && <form onSubmit={tokenForm.handleSubmit(value => connect.mutate(value))}><h2>Pagamentos</h2><div className="field"><Label htmlFor="pushinpay-token">Token PushinPay</Label><Input id="pushinpay-token" type="password" autoComplete="off" {...tokenForm.register('token')} /><p className="field-help">O token é criptografado e não poderá ser visualizado depois.</p>{tokenForm.formState.errors.token && <p role="alert">{tokenForm.formState.errors.token.message}</p>}</div><Button disabled={connect.isPending}>Conectar PushinPay</Button>{account && <p>PushinPay conectada · token final {account.tokenFingerprint}</p>}<ErrorNotice error={connect.error} /></form>}</div><ErrorNotice error={rename.error} /></section>
}

function Workspace({ id, userId, organizations, onLostAccess }: { id: string; userId: string; organizations: Array<{ id: string; name: string; role: Role }>; onLostAccess: () => void }) {
  const route = useCatalogLocation(); const detail = useQuery({ queryKey: ['organizations', userId, id, 'details'], queryFn: () => api.get(id), retry: false })
  if (detail.isPending) return <p role="status">Abrindo organização...</p>
  if (detail.error) return <div><ErrorNotice error={detail.error} /><Button variant="outline" onClick={onLostAccess}>Voltar às organizações</Button></div>
  const organization = detail.data!; const view = route.view === 'organizations' ? 'overview' : route.view
  return <section className="workspace-shell" aria-label={`Organização ${organization.name}`}><aside className="workspace-rail"><div className="rail-organization"><Label htmlFor="active-org">Organização</Label><select id="active-org" value={id} onChange={event => navigateWorkspace('/dashboard', event.target.value)}>{organizations.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><span>{roleNames[organization.role]}</span></div><nav aria-label="Navegação da organização">{destinations.map((destination, index) => { const Icon = destination.icon; return <button key={destination.view} className={view === destination.view ? 'active' : ''} onClick={() => navigateWorkspace(destination.path, id)}><span>0{index + 1}</span><Icon /><strong>{destination.label}</strong></button> })}</nav></aside><div className="workspace-canvas">{view === 'overview' && <WorkspaceOverview org={id} userId={userId} organizationName={organization.name} />}{(view === 'products' || view === 'offers') && <Catalog org={id} userId={userId} role={organization.role} />}{view === 'customers' && <Customers org={id} userId={userId} />}{view === 'team' && <Team id={id} userId={userId} actor={organization.role} />}{view === 'settings' && <OrganizationSettings id={id} userId={userId} organization={organization} />}</div></section>
}

export function Organizations({ userId }: { userId: string }) {
  const client = useQueryClient(); const [page, setPage] = useState(1); const route = useCatalogLocation(); const [chosen, setSelected] = useState(''); const selected = route.org || chosen; const [name, setName] = useState(''); const list = useQuery({ queryKey: ['organizations', userId, 'list', page], queryFn: () => api.list(page), retry: false })
  const create = useMutation({ mutationFn: () => api.create(name.trim()), onSuccess: async organization => { setName(''); setSelected(organization.id); navigateWorkspace('/dashboard', organization.id); await client.invalidateQueries({ queryKey: ['organizations', userId] }) } })
  if (list.isPending) return <p role="status">Carregando organizações...</p>; if (list.error) return <><ErrorNotice error={list.error} /><Button variant="outline" onClick={() => void list.refetch()}>Recarregar organizações</Button></>
  const organizations = list.data?.items ?? []; if (selected && organizations.some(item => item.id === selected)) return <Workspace key={selected} id={selected} userId={userId} organizations={organizations} onLostAccess={() => { setSelected(''); navigateWorkspace('/', ''); void client.invalidateQueries({ queryKey: ['organizations', userId] }) }} />
  return <section className="organization-entry">
    <div className="organization-heading">
      <div><p className="entry-kicker"><span />Comece por aqui</p><h1>Organizações</h1><p>Escolha um espaço existente ou crie sua operação para começar a gerenciar seus fluxos.</p></div>
      <div className="organization-count" aria-label={`${list.data?.total ?? 0} organizações disponíveis`}><strong>{String(list.data?.total ?? 0).padStart(2, '0')}</strong><span>{list.data?.total === 1 ? 'Disponível' : 'Disponíveis'}</span></div>
    </div>
    {organizations.length > 0 && <section className="available-organizations" aria-labelledby="available-organizations-title">
      <div className="organization-section-label"><h2 id="available-organizations-title">Suas organizações <span>{list.data?.total ?? organizations.length}</span></h2><small>Ambiente de produção</small></div>
      <div className="organization-list">{organizations.map(item => <article className="organization-card" key={item.id}>
        <div className="organization-card-main"><span className="organization-icon"><Building2 /></span><div><div className="organization-card-title"><h3>{item.name}</h3><span>{roleNames[item.role]}</span><small><i />Ativo</small></div><p>ID: <code>{item.id}</code></p></div></div>
        <Button className="enter-organization" variant="outline" aria-label={`Entrar no espaço ${item.name}`} onClick={() => { setSelected(item.id); navigateWorkspace('/dashboard', item.id) }}>Entrar no espaço<ArrowRight /></Button>
      </article>)}</div>
      <Pagination data={list.data} page={page} setPage={setPage} />
    </section>}
    <section className="create-organization-panel">
      <div className="create-organization-heading"><span><Plus /></span><div><h2 id="create-organization-title">Nova organização</h2><p>Defina um ambiente isolado para seus projetos e sua equipe.</p></div></div>
      <form className="create-organization" onSubmit={event => { event.preventDefault(); create.mutate() }}><Label htmlFor="new-org">Nome da organização</Label><div><Input id="new-org" aria-label="Nova organização" required maxLength={100} placeholder="Ex: Minha empresa" value={name} onChange={event => setName(event.target.value)} /><Button aria-label="Criar" disabled={create.isPending || !name.trim()}><Plus />{create.isPending ? 'Criando...' : 'Criar organização'}</Button></div><p className="organization-help"><Info />Você poderá convidar membros e configurar permissões após a criação.</p></form>
      <ErrorNotice error={create.error} />
    </section>
  </section>
}
