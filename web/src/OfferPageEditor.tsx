import { useState, useId, useEffect } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useForm, useFieldArray } from 'react-hook-form'
import { pageClient, type PageContent, type AutomationStep, type AutomationStepType } from './lib/page-client'
import { Button } from './components/ui/button'
import { Input } from './components/ui/input'
import { Label } from './components/ui/label'
import { ApiError } from './lib/auth-client'
type LinkForm = { name: string; url: string }
function FulfillmentEditor({ org, offer }: { org: string; offer: string }) {
  const current = useQuery({ queryKey: ['organizations', org, 'external-link', offer], queryFn: () => pageClient.externalLink(org, offer), retry: false })
  const form = useForm<LinkForm>({ defaultValues: { name: '', url: '' } })
  useEffect(() => { if (current.data) form.reset({ name: current.data.name, url: current.data.url }) }, [current.data, form])
  const save = useMutation({ mutationFn: (data: LinkForm) => pageClient.saveExternalLink(org, offer, data) })
  if (current.isPending) return <p role="status">Carregando entrega...</p>
  if (current.error && !(current.error instanceof ApiError && current.error.status === 404)) return <p role="alert">{current.error.message}</p>
  return <form className="catalog-form page-editor" onSubmit={form.handleSubmit(data => save.mutate(data))}><h3>Entrega por link</h3><p className="field-help">O comprador verá este acesso depois que o PIX for confirmado.</p>
    <div className="field"><Label htmlFor="fulfillment-name">Nome do acesso</Label><Input id="fulfillment-name" required maxLength={100} {...form.register('name')} /></div>
    <div className="field"><Label htmlFor="fulfillment-url">Link HTTPS</Label><Input id="fulfillment-url" type="url" pattern="https://.*" required maxLength={2048} {...form.register('url')} /></div>
    {save.error && <p role="alert" className="error-notice">{save.error.message}</p>}{save.isSuccess && <p role="status">Entrega salva.</p>}<Button disabled={save.isPending}>Salvar entrega</Button>
  </form>
}
function DigitalFileEditor({ org, offer }: { org: string; offer: string }) {
  const current = useQuery({ queryKey: ['organizations', org, 'digital-file', offer], queryFn: () => pageClient.digitalFile(org, offer), retry: false })
  const upload = useMutation({ mutationFn: (file: File) => pageClient.saveDigitalFile(org, offer, file) })
  const value = upload.data ?? current.data
  return <section className="catalog-form page-editor"><h3>Arquivo digital protegido</h3><p className="field-help">PDF, ZIP, EPUB, planilha, PNG ou JPEG. Limite de 25 MiB. O download exige uma compra ativa.</p>
    {value && <div className="digital-file-current"><strong>{value.name}</strong><span>{value.contentType} · {(value.size / 1024 / 1024).toFixed(2)} MiB</span></div>}
    <div className="field"><Label htmlFor="fulfillment-file">{value ? 'Substituir arquivo' : 'Selecionar arquivo'}</Label><Input id="fulfillment-file" type="file" accept=".pdf,.zip,.epub,.xlsx,.png,.jpg,.jpeg" onChange={event => { const file = event.target.files?.[0]; if (file) upload.mutate(file) }} /></div>
    {upload.isPending && <p role="status">Enviando arquivo...</p>}{upload.error && <p role="alert" className="error-notice">{upload.error.message}</p>}{upload.isSuccess && <p role="status">Arquivo protegido salvo.</p>}
  </section>
}
function TelegramEditor({ org, offer }: { org: string; offer: string }) {
  const form = useForm<{ name: string; chatId: string }>({ defaultValues: { name: 'Acessar comunidade', chatId: '' } })
  const save = useMutation({ mutationFn: (data: { name: string; chatId: string }) => pageClient.saveTelegram(org, offer, data) })
  return <form className="catalog-form page-editor" onSubmit={form.handleSubmit(data => save.mutate(data))}><h3>Comunidade no Telegram</h3><p className="field-help">Adicione o bot como administrador e informe o ID do grupo/canal privado ou @username.</p>
    <div className="field"><Label htmlFor="telegram-name">Nome da comunidade</Label><Input id="telegram-name" required maxLength={100} {...form.register('name')} /></div>
    <div className="field"><Label htmlFor="telegram-chat">Grupo ou canal</Label><Input id="telegram-chat" required maxLength={100} placeholder="-1001234567890" {...form.register('chatId')} /></div>
    {save.error && <p role="alert" className="error-notice">{save.error.message}</p>}{save.isSuccess && <p role="status">Entrega Telegram salva.</p>}<Button disabled={save.isPending}>Salvar Telegram</Button>
  </form>
}
const stepNames: Record<AutomationStepType, string> = { SEND_TELEGRAM_MESSAGE: 'Enviar mensagem', SEND_OFFER: 'Apresentar oferta', CHECKOUT: 'Enviar checkout', CONDITION: 'Verificar condição', WAIT: 'Aguardar', TELEGRAM_ACCESS: 'Liberar acesso Telegram', END: 'Encerrar funil' }
function AutomationEditor({ org, offer }: { org: string; offer: string }) {
  const current = useQuery({ queryKey: ['organizations', org, 'automation', offer], queryFn: () => pageClient.automation(org, offer), retry: false })
  const [editedSteps, setSteps] = useState<AutomationStep[] | null>(null)
  const steps = editedSteps ?? (current.data?.steps.length ? current.data.steps : [{ type: 'SEND_TELEGRAM_MESSAGE', text: 'Olá, {{buyer_name}}! Sua compra foi confirmada.' }, { type: 'END' }]) as AutomationStep[]
  const save = useMutation({ mutationFn: () => pageClient.saveAutomation(org, offer, steps) })
  const publish = useMutation({ mutationFn: async () => { await pageClient.saveAutomation(org, offer, steps); return pageClient.publishAutomation(org, offer) }, onSuccess: data => current.refetch().then(() => data) })
  const change = (index: number, patch: Partial<AutomationStep>) => setSteps(steps.map((step, position) => position === index ? { ...step, ...patch } : step))
  const move = (index: number, direction: -1 | 1) => { const target = index + direction; if (target < 0 || target >= steps.length) return; const copy = [...steps]; [copy[index], copy[target]] = [copy[target], copy[index]]; setSteps(copy) }
  if (current.isPending) return <p role="status">Carregando funil...</p>
  if (current.error) return <p role="alert">{current.error.message}</p>
  return <section className="catalog-form page-editor"><div><h3>Funil de mensagens</h3><p className="field-help">Defina a sequência que o seu bot envia ao comprador. Use {'{{buyer_name}}'}, {'{{buyer_email}}'}, {'{{offer_name}}'} ou {'{{checkout_url}}'} nas mensagens.</p>{current.data?.publishedVersion ? <p className="field-help">Versão {current.data.publishedVersion} publicada.</p> : <p className="field-help">Ainda não publicado.</p>}</div>
    {steps.map((step, index) => <fieldset className="page-block" key={`${index}-${step.id ?? 'new'}`}><legend>{index + 1}. {stepNames[step.type]}</legend>
      <Label htmlFor={`automation-type-${index}`}>Ação</Label><select id={`automation-type-${index}`} value={step.type} onChange={event => change(index, { type: event.target.value as AutomationStepType, text: '', url: '', waitMinutes: null, condition: null })}>{Object.entries(stepNames).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
      {step.type === 'SEND_TELEGRAM_MESSAGE' && <><Label htmlFor={`automation-text-${index}`}>Mensagem</Label><textarea id={`automation-text-${index}`} required maxLength={4000} value={step.text ?? ''} onChange={event => change(index, { text: event.target.value })} /></>}
      {(step.type === 'SEND_OFFER' || step.type === 'CHECKOUT') && <><Label htmlFor={`automation-url-${index}`}>URL opcional</Label><Input id={`automation-url-${index}`} type="url" pattern="https://.*" placeholder="Usa a página desta oferta quando vazio" value={step.url ?? ''} onChange={event => change(index, { url: event.target.value })} /></>}
      {step.type === 'WAIT' && <><Label htmlFor={`automation-wait-${index}`}>Minutos</Label><Input id={`automation-wait-${index}`} type="number" min={1} max={43200} value={step.waitMinutes ?? 1} onChange={event => change(index, { waitMinutes: Number(event.target.value) })} /></>}
      {step.type === 'CONDITION' && <><Label htmlFor={`automation-condition-${index}`}>Continuar somente se</Label><select id={`automation-condition-${index}`} value={step.condition ?? 'PAYMENT_CONFIRMED'} onChange={event => change(index, { condition: event.target.value })}><option value="PAYMENT_CONFIRMED">Pagamento confirmado</option><option value="ENTITLEMENT_ACTIVE">Acesso ativo</option></select></>}
      <div className="catalog-actions"><Button type="button" variant="outline" disabled={index === 0} onClick={() => move(index, -1)}>Subir</Button><Button type="button" variant="outline" disabled={index === steps.length - 1} onClick={() => move(index, 1)}>Descer</Button><Button type="button" variant="outline" disabled={steps.length === 1} onClick={() => setSteps(steps.filter((_, position) => position !== index))}>Remover</Button></div>
    </fieldset>)}
    <div className="catalog-actions"><Button type="button" variant="outline" disabled={steps.length >= 30} onClick={() => setSteps([...steps, { type: 'SEND_TELEGRAM_MESSAGE', text: '' }])}>Adicionar etapa</Button><Button type="button" variant="outline" disabled={save.isPending} onClick={() => save.mutate()}>Salvar rascunho</Button><Button type="button" disabled={publish.isPending} onClick={() => publish.mutate()}>Publicar funil</Button></div>
    {(save.error || publish.error) && <p role="alert" className="error-notice">{(save.error ?? publish.error)?.message}</p>}{save.isSuccess && !publish.isPending && <p role="status">Rascunho salvo.</p>}{publish.isSuccess && <p role="status">Funil publicado com sucesso.</p>}
  </section>
}
function PageForm({ org, offer, initial }: { org: string; offer: string; initial: PageContent }) {
  const form = useForm<PageContent>({ defaultValues: initial })
  const [benefits, setBenefits] = useState(initial.benefits.join('\n'))
  const testimonials = useFieldArray({ control: form.control, name: 'testimonials' })
  const faq = useFieldArray({ control: form.control, name: 'faq' })
  const fields = useFieldArray({ control: form.control, name: 'fields' })
  const save = useMutation({ mutationFn: (data: PageContent) => pageClient.save(org, offer, { ...data, benefits: benefits.split('\n').map(x => x.trim()).filter(Boolean) }) })
  return <form className="catalog-form page-editor" onSubmit={form.handleSubmit(data => save.mutate(data))}><h3>Página pública</h3><p className="field-help">O preço vem da oferta. A página fica pública quando produto e oferta estão ativos. Conteúdo em texto, sem HTML.</p>
    <div className="field"><Label htmlFor="page-title">Título da página</Label><Input id="page-title" required maxLength={300} {...form.register('title')} /></div>
    <div className="field"><Label htmlFor="page-subtitle">Subtítulo</Label><Input id="page-subtitle" maxLength={500} {...form.register('subtitle')} /></div>
    {(['imageUrl', 'videoUrl', 'logoUrl'] as const).map((key, index) => <div className="field" key={key}><Label htmlFor={`page-${key}`}>{['Imagem (URL HTTPS)', 'Vídeo (URL HTTPS do arquivo)', 'Logo (URL HTTPS)'][index]}</Label><Input id={`page-${key}`} type="url" pattern="https://.*" maxLength={2048} {...form.register(key)} /></div>)}
    <div className="field"><Label htmlFor="page-color">Cor de destaque</Label><Input id="page-color" type="color" {...form.register('color')} /></div>
    <div className="field"><Label htmlFor="page-description">Descrição da página</Label><textarea id="page-description" maxLength={10000} {...form.register('description')} /></div>
    <div className="field"><Label htmlFor="page-benefits">Benefícios (um por linha, até 20)</Label><textarea id="page-benefits" maxLength={10020} value={benefits} onChange={event => setBenefits(event.target.value)} /></div>
    <fieldset><legend>Depoimentos</legend>{testimonials.fields.map((item, index) => <div className="page-block" key={item.id}><Label htmlFor={`testimonial-name-${index}`}>Nome {index + 1}</Label><Input id={`testimonial-name-${index}`} required maxLength={100} {...form.register(`testimonials.${index}.name`)} /><Label htmlFor={`testimonial-text-${index}`}>Depoimento {index + 1}</Label><textarea id={`testimonial-text-${index}`} required maxLength={2000} {...form.register(`testimonials.${index}.text`)} /><Button type="button" variant="outline" onClick={() => testimonials.remove(index)}>Remover depoimento {index + 1}</Button></div>)}<Button type="button" variant="outline" disabled={testimonials.fields.length >= 10} onClick={() => testimonials.append({ name: '', text: '' })}>Adicionar depoimento</Button></fieldset>
    <fieldset><legend>Perguntas frequentes</legend>{faq.fields.map((item, index) => <div className="page-block" key={item.id}><Label htmlFor={`faq-question-${index}`}>Pergunta {index + 1}</Label><Input id={`faq-question-${index}`} required maxLength={300} {...form.register(`faq.${index}.question`)} /><Label htmlFor={`faq-answer-${index}`}>Resposta {index + 1}</Label><textarea id={`faq-answer-${index}`} required maxLength={2000} {...form.register(`faq.${index}.answer`)} /><Button type="button" variant="outline" onClick={() => faq.remove(index)}>Remover pergunta {index + 1}</Button></div>)}<Button type="button" variant="outline" disabled={faq.fields.length >= 20} onClick={() => faq.append({ question: '', answer: '' })}>Adicionar pergunta</Button></fieldset>
    <div className="field"><Label htmlFor="page-guarantee">Garantia</Label><textarea id="page-guarantee" maxLength={2000} {...form.register('guarantee')} /></div>
    <div className="field"><Label htmlFor="page-cta">Texto do botão</Label><Input id="page-cta" required maxLength={80} {...form.register('cta')} /></div>
    <fieldset><legend>Campos adicionais do checkout</legend>{fields.fields.map((item, index) => <div className="page-block" key={item.id}><Label htmlFor={`field-key-${index}`}>Identificador do campo {index + 1}</Label><Input id={`field-key-${index}`} required pattern="[a-z][a-z0-9_]{0,39}" maxLength={40} {...form.register(`fields.${index}.key`)} /><Label htmlFor={`field-label-${index}`}>Rótulo do campo {index + 1}</Label><Input id={`field-label-${index}`} required maxLength={100} {...form.register(`fields.${index}.label`)} /><Label htmlFor={`field-type-${index}`}>Tipo do campo {index + 1}</Label><select id={`field-type-${index}`} {...form.register(`fields.${index}.type`)}><option value="text">Texto curto</option><option value="textarea">Texto longo</option></select><label><input type="checkbox" {...form.register(`fields.${index}.required`)} /> Obrigatório</label><Button type="button" variant="outline" onClick={() => fields.remove(index)}>Remover campo {index + 1}</Button></div>)}<Button type="button" variant="outline" disabled={fields.fields.length >= 10} onClick={() => fields.append({ key: '', label: '', type: 'text', required: false })}>Adicionar campo</Button></fieldset>
    {save.error && <p role="alert" className="error-notice">{save.error.message}</p>}{save.isSuccess && <p role="status">Página salva.</p>}<Button disabled={save.isPending}>Salvar página</Button>
  </form>
}
function LoadedPageEditor({ org, offer, userId }: { org: string; offer: string; userId: string }) {
  const instance = useId()
  const page = useQuery({ queryKey: ['organizations', userId, org, 'offer-page', offer, instance], queryFn: () => pageClient.get(org, offer), staleTime: 0, gcTime: 0, retry: false })
  return page.isPending ? <p role="status">Carregando página...</p> : page.error ? <p role="alert">{page.error.message}</p> : page.data && <PageForm org={org} offer={offer} initial={page.data} />
}
export function OfferPageEditor({ org, offer, userId, slug }: { org: string; offer: string; userId: string; slug: string }) {
  const [open, setOpen] = useState(false)
  const [deliveryOpen, setDeliveryOpen] = useState(false)
  const [automationOpen, setAutomationOpen] = useState(false)
  return <section className="page-editor-section"><div className="catalog-actions"><Button variant="outline" onClick={() => setOpen(!open)}>{open ? 'Fechar editor da página' : 'Editar página pública'}</Button><Button variant="outline" onClick={() => setDeliveryOpen(!deliveryOpen)}>{deliveryOpen ? 'Fechar entrega' : 'Configurar entrega'}</Button><Button variant="outline" onClick={() => setAutomationOpen(!automationOpen)}>{automationOpen ? 'Fechar funil' : 'Editar funil de mensagens'}</Button><a href={`/o/${encodeURIComponent(slug)}`} target="_blank" rel="noopener noreferrer">Ver página pública</a></div>{open && <LoadedPageEditor org={org} offer={offer} userId={userId} />}{deliveryOpen && <><FulfillmentEditor org={org} offer={offer} /><DigitalFileEditor org={org} offer={offer} /><TelegramEditor org={org} offer={offer} /></>}{automationOpen && <AutomationEditor org={org} offer={offer} />}</section>
}
