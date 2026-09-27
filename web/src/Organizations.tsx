import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, Plus, Users } from 'lucide-react'
import { ApiError } from './lib/auth-client'
import { organizationClient as api, roleNames, type Role, type Member, type Page } from './lib/organization-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'

function ErrorNotice({ error }: { error: Error | null }) {
  return error ? <p role="alert" className="error-notice">{error instanceof ApiError ? error.message : 'Não foi possível conectar. Tente novamente.'}</p> : null
}
function Pagination({ data, page, setPage }: { data?: Page<unknown>; page: number; setPage: (page: number) => void }) {
  if (!data || data.total <= data.pageSize) return null
  return <div className="pagination"><Button variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page} de {Math.ceil(data.total / data.pageSize)}</span><Button variant="outline" disabled={page * data.pageSize >= data.total} onClick={() => setPage(page + 1)}>Próxima</Button></div>
}
const roles = Object.keys(roleNames) as Role[]
function RoleOptions({ owner }: { owner: boolean }) {
  return roles.filter(role => owner || !['OWNER', 'ADMIN'].includes(role)).map(role => <option key={role} value={role}>{roleNames[role]}</option>)
}
function MemberRow({ member, actor, pending, change, remove }: { member: Member; actor: Role; pending: boolean; change: (role: Role) => void; remove: () => void }) {
  const [role, setRole] = useState(member.role)
  const canManage = actor === 'OWNER' || actor === 'ADMIN' && !['OWNER', 'ADMIN'].includes(member.role)
  const [confirm, setConfirm] = useState(false)
  return <li className="member-row"><div className="member-identity"><strong>{member.displayName}</strong><span>{member.email}</span></div>
    {canManage ? <div className="member-actions"><select aria-label={`Papel de ${member.displayName}`} value={role} onChange={event => setRole(event.target.value as Role)} disabled={pending}><RoleOptions owner={actor === 'OWNER'} /></select>
      <Button variant="outline" disabled={pending || role === member.role} onClick={() => change(role)}>Salvar papel</Button>
      {confirm ? <><span>Remover acesso?</span><Button variant="outline" disabled={pending} onClick={remove}>Confirmar remoção</Button><Button variant="ghost" disabled={pending} onClick={() => setConfirm(false)}>Cancelar</Button></> : <Button variant="ghost" disabled={pending} onClick={() => setConfirm(true)}>Remover</Button>}
    </div> : <span className="role-badge">{roleNames[member.role]}</span>}
  </li>
}
function Workspace({ id, userId, onLostAccess }: { id: string; userId: string; onLostAccess: () => void }) {
  const client = useQueryClient()
  const [page, setPage] = useState(1)
  const detail = useQuery({ queryKey: ['organizations', userId, id, 'details'], queryFn: () => api.get(id), retry: false })
  const members = useQuery({ queryKey: ['organizations', userId, id, 'members', page], queryFn: () => api.members(id, page), retry: false })
  const [name, setName] = useState<string | null>(null)
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<Role>('OPERATOR')
  const assignableRole = detail.data?.role === 'ADMIN' && ['OWNER', 'ADMIN'].includes(role) ? 'OPERATOR' : role
  async function invalidate() { await client.invalidateQueries({ queryKey: ['organizations', userId] }) }
  const rename = useMutation({ mutationFn: () => api.rename(id, name ?? detail.data!.name), onSuccess: async () => { setName(null); await invalidate() } })
  const add = useMutation({ mutationFn: () => api.add(id, email.trim(), assignableRole), onSuccess: async () => { setEmail(''); await invalidate() } })
  const change = useMutation({ mutationFn: ({ memberId, role }: { memberId: string; role: Role }) => api.change(id, memberId, role), onSettled: invalidate })
  const remove = useMutation({ mutationFn: (memberId: string) => api.remove(id, memberId), onSettled: invalidate })
  if (detail.isPending) return <p role="status">Abrindo organização...</p>
  if (detail.error) return <div><ErrorNotice error={detail.error} /><Button variant="outline" onClick={onLostAccess}>Voltar às organizações</Button><Button variant="outline" onClick={() => void detail.refetch()}>Tentar novamente</Button></div>
  const organization = detail.data!
  const canManage = organization.role === 'OWNER' || organization.role === 'ADMIN'
  return <section className="workspace" aria-label={`Organização ${organization.name}`}>
    <div className="section-heading"><div><p className="eyebrow">Organização ativa</p><h2>{organization.name}</h2></div><span className="role-badge">{roleNames[organization.role]}</span></div>
    {canManage && <form className="inline-form" onSubmit={event => { event.preventDefault(); rename.mutate() }}><div className="field"><Label htmlFor="org-name">Nome da organização</Label><Input id="org-name" maxLength={100} required value={name ?? organization.name} onChange={event => setName(event.target.value)} /></div><Button variant="outline" disabled={rename.isPending || !(name ?? organization.name).trim()}>Salvar nome</Button></form>}
    <ErrorNotice error={rename.error} />
    <div className="team-heading"><Users size={20} /><h3>Equipe</h3></div>
    <p className="section-copy">Cada pessoa acessa esta organização de acordo com seu papel.</p>
    {canManage && <form className="member-form" onSubmit={event => { event.preventDefault(); add.mutate() }}><div className="field"><Label htmlFor="member-email">E-mail da pessoa</Label><Input id="member-email" type="email" required maxLength={254} placeholder="pessoa@exemplo.com" value={email} onChange={event => setEmail(event.target.value)} /><p className="field-help">A pessoa precisa ter uma conta na Zyven.</p></div><div className="field"><Label htmlFor="new-role">Papel</Label><select id="new-role" value={assignableRole} onChange={event => setRole(event.target.value as Role)}><RoleOptions owner={organization.role === 'OWNER'} /></select></div><Button disabled={add.isPending}><Plus size={16} />Adicionar membro</Button></form>}
    <ErrorNotice error={add.error ?? change.error ?? remove.error} />
    {members.isPending ? <p role="status">Carregando equipe...</p> : members.error ? <><ErrorNotice error={members.error} /><Button variant="outline" onClick={() => void members.refetch()}>Recarregar equipe</Button></> : <><ul className="member-list">{members.data?.items.map(member => <MemberRow key={`${member.id}-${member.role}`} member={member} actor={organization.role} pending={change.isPending || remove.isPending} change={role => change.mutate({ memberId: member.id, role })} remove={() => remove.mutate(member.id)} />)}</ul><Pagination data={members.data} page={page} setPage={setPage} /></>}
  </section>
}
export function Organizations({ userId }: { userId: string }) {
  const client = useQueryClient()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState('')
  const [name, setName] = useState('')
  const list = useQuery({ queryKey: ['organizations', userId, 'list', page], queryFn: () => api.list(page), retry: false })
  const create = useMutation({ mutationFn: () => api.create(name.trim()), onSuccess: async organization => { setName(''); setSelected(organization.id); await client.invalidateQueries({ queryKey: ['organizations', userId] }) } })
  return <section className="organizations-panel"><div className="section-heading"><div><p className="eyebrow">Seu trabalho, em equipe</p><h2>Suas organizações</h2></div><Building2 size={28} /></div>
    <p className="section-copy">Escolha um espaço para trabalhar ou crie uma nova organização.</p>
    {list.isPending ? <p role="status">Carregando organizações...</p> : list.error ? <><ErrorNotice error={list.error} /><Button variant="outline" onClick={() => void list.refetch()}>Recarregar organizações</Button></> : <>
      {!list.data?.total && <p>Você ainda não participa de uma organização.</p>}
      {!!list.data?.items.length && <div className="field"><Label htmlFor="active-org">Organização ativa</Label><select id="active-org" value={list.data.items.some(item => item.id === selected) ? selected : ''} onChange={event => setSelected(event.target.value)}><option value="">Selecione uma organização</option>{list.data.items.map(item => <option key={item.id} value={item.id}>{item.name} · {roleNames[item.role]}</option>)}</select></div>}
      <Pagination data={list.data} page={page} setPage={setPage} />
    </>}
    <form className="inline-form create-org" onSubmit={event => { event.preventDefault(); create.mutate() }}><div className="field"><Label htmlFor="new-org">Nome da nova organização</Label><Input id="new-org" required maxLength={100} placeholder="Ex.: Estúdio Aurora" value={name} onChange={event => setName(event.target.value)} /></div><Button disabled={create.isPending || !name.trim()}><Plus size={16} />Criar organização</Button></form>
    <ErrorNotice error={create.error} />
    {selected && <Workspace key={selected} id={selected} userId={userId} onLostAccess={() => { setSelected(''); void client.invalidateQueries({ queryKey: ['organizations', userId] }) }} />}
  </section>
}
