# Fase 2 — Organizations, Multi-tenancy e Roles

Pré-condição: todos os gates da fase 1 aprovados e commit de fechamento registrado.

## Escopo autorizado

Organization possui Id, Name, CreatedAt e UpdatedAt. OrganizationMember possui Id, OrganizationId, UserId, Role e CreatedAt, com vínculo único por organização/usuário. Papéis: OWNER, ADMIN, OPERATOR, FINANCE e SUPPORT. Um usuário participa de várias organizações.

## Contrato e limites

- `GET/POST /api/organizations`: listar somente vínculos do usuário; criar organização e seu OWNER na mesma transação.
- `GET/PATCH /api/organizations/{id}`: leitura para membros, alteração de nome para OWNER/ADMIN. Não-membros recebem 404.
- `GET/POST /api/organizations/{id}/members`: listar equipe e adicionar usuário já cadastrado por e-mail. Não enviar convites ou mensagens externas.
- `PATCH/DELETE /api/organizations/{id}/members/{memberId}`: gerenciamento transacional. OWNER gerencia papéis; ADMIN gerencia apenas OPERATOR/FINANCE/SUPPORT. Nunca remover ou rebaixar o último OWNER. Operações concorrentes devem preservar essa regra com bloqueio da linha da organização.
- Nunca confiar em OrganizationId do cliente sem consultar o vínculo persistido. Criar serviço de autorização reutilizável para fases comerciais; toda consulta e alteração de membros inclui OrganizationId.

## Interface

Após autenticação, listar organizações e permitir criar uma. Organização selecionada apenas em memória; cache de dados inclui seu identificador. Exibir seletor de organização, dados básicos e equipe, com ações condicionadas ao papel e validação autoritativa no backend. Não exibir páginas comerciais ainda indisponíveis.

## Verificação

- [ ] Testes primeiro: criar/listar, múltiplos vínculos, ausência de sessão, não-membro sem acesso, ID de membro de outra organização, matriz de permissões, proteção do último proprietário e concorrência.
- [ ] Migration PostgreSQL com índices de membros e chaves estrangeiras.
- [ ] Frontend: seleção/criação de organização, gerenciamento permitido de equipe, testes de componentes.
- [ ] Build backend/frontend, testes unitários e de integração reais, lint/formatação, migrations e smoke Compose.
- [ ] Revisão de especificação e segurança, documentação e commits pequenos antes da fase 3.

A fase 3 adicionará Products e Offers respeitando esta fronteira de autorização.
