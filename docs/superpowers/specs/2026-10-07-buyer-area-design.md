# Área do comprador

## Objetivo

Criar uma biblioteca unificada onde uma pessoa recupera todas as compras pagas feitas com o mesmo e-mail na Zyven. O acesso usa um código temporário enviado por e-mail e não depende do navegador usado no checkout.

## Escopo

A primeira versão inclui solicitação e validação de código, sessão do comprador, logout, listagem de compras, consulta de uma compra e recuperação da entrega `EXTERNAL_LINK`. O acesso existente por cookie do checkout continua válido. Personalização por vendedor, arquivos protegidos, streaming, progresso de curso e assinaturas ficam fora desta etapa.

## Identidade e autenticação

O e-mail normalizado é a identidade global do comprador. Solicitar acesso sempre devolve uma resposta neutra, exista ou não uma compra, para impedir enumeração. Quando houver pelo menos um customer associado ao e-mail, o sistema gera um código numérico de seis dígitos, armazena somente seu hash e registra expiração, tentativas e consumo.

O código expira em dez minutos, aceita no máximo cinco tentativas e só pode ser usado uma vez. Uma validação bem-sucedida cria uma sessão aleatória de 30 dias em cookie `HttpOnly`, `Secure` fora do desenvolvimento, `SameSite=Lax`. Apenas o hash do token da sessão fica no banco. Solicitação e verificação têm limitação de taxa por IP e e-mail normalizado.

O envio é isolado por uma interface de entrega de código. Em desenvolvimento, o provedor grava o código no log protegido da aplicação para permitir smoke tests; produção exige um provedor configurado e nunca devolve o código na resposta HTTP.

## Biblioteca e autorização

As consultas partem exclusivamente do e-mail confirmado na sessão. A API localiza customers de todas as organizações por `NormalizedEmail`, mas retorna somente pagamentos `PAID` ligados a entitlements `ACTIVE`. Cada item inclui oferta, organização vendedora, data da compra e entregas concluídas.

Abrir uma compra ou entrega refaz a associação entre sessão, customer, pagamento e entitlement. Um identificador de pagamento conhecido não concede acesso. Recursos ausentes ou pertencentes a outra pessoa respondem como não encontrados.

## Experiência

As rotas públicas são `/buyer/login` e `/buyer/purchases`. O login pede e-mail e depois o código. A biblioteca mostra cartões de compra, estado vazio e sessão expirada. Cada cartão apresenta oferta, vendedor, data, estado do acesso e a ação para abrir o conteúdo. A interface segue o tema escuro atual e funciona em telas pequenas.

Após uma confirmação de pagamento no checkout, o comprador continua vendo a entrega imediatamente. A página também oferece a entrada para “Minhas compras”, permitindo recuperar o acesso em outro dispositivo.

## API e dados

Novas entidades: `BuyerAccessCode` e `BuyerSession`. Novos endpoints públicos:

- `POST /api/buyer/auth/request-code`
- `POST /api/buyer/auth/verify-code`
- `POST /api/buyer/auth/logout`
- `GET /api/buyer/me`
- `GET /api/buyer/purchases`
- `GET /api/buyer/purchases/{paymentId}`

Os endpoints de consulta usam a sessão do comprador e não compartilham JWT ou permissões da área do vendedor.

## Erros e observabilidade

Respostas de solicitação não revelam cadastro. Código inválido, expirado, consumido ou bloqueado usa uma mensagem pública única. Logs registram eventos e identificadores internos sem e-mail completo, código ou token. Falha no provedor de e-mail não cria uma sessão e pode ser repetida com segurança.

## Validação

Testes cobrem expiração, uso único, limite de tentativas, resposta neutra, hash em repouso, isolamento entre compradores e organizações, pagamentos pendentes, entitlement revogado, logout, cookie seguro, acesso legado pelo checkout e fluxo React de login, biblioteca e entrega. A etapa termina com build, migração, testes .NET, testes do frontend, lint, smoke em Docker Compose e inspeção visual responsiva.
