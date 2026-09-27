# Organizações e isolamento

Uma conta pode participar de várias organizações. A API extrai UserId do JWT validado e consulta OrganizationMember no PostgreSQL; não aceita papéis ou identidade fornecidos pelo cliente como autorização.

| Ação | OWNER | ADMIN | OPERATOR / FINANCE / SUPPORT |
| --- | --- | --- | --- |
| Ler organização e equipe | Sim | Sim | Sim |
| Alterar nome | Sim | Sim | Não |
| Adicionar/alterar/remover OWNER ou ADMIN | Sim | Não | Não |
| Gerenciar OPERATOR, FINANCE e SUPPORT | Sim | Sim | Não |

Não-membros recebem 404. IDs de membro são consultados junto do OrganizationId, impedindo usar a rota de uma organização para alterar outra. O último OWNER não pode ser removido ou rebaixado. Alterações bloqueiam a linha da organização antes de consultar permissões e contar proprietários; a mudança e o evento de auditoria são persistidos na mesma transação.

## API

- `GET /api/organizations?page=1&pageSize=20`: organizações do usuário.
- `POST /api/organizations`: `{ "name": "Meu estúdio" }`; cria organização e vínculo OWNER atomicamente.
- `GET/PATCH /api/organizations/{id}`: detalhes ou alteração de nome.
- `GET /api/organizations/{id}/members?page=1&pageSize=20`: equipe.
- `POST /api/organizations/{id}/members`: `{ "email": "pessoa@example.com", "role": "OPERATOR" }`; exige uma conta existente.
- `PATCH /api/organizations/{id}/members/{memberId}`: `{ "role": "SUPPORT" }`.
- `DELETE /api/organizations/{id}/members/{memberId}`: remove acesso à organização.

Listas retornam `{ items, page, pageSize, total }`, com pageSize entre 1 e 100 e validação contra overflow do deslocamento. Adicionar equipe não envia convite, e-mail ou outra mensagem externa.

No frontend, a navegação do catálogo carrega o contexto de organização na URL; os dados do workspace usam chaves de cache com UserId e OrganizationId. A autorização da interface é apenas apresentação: a API revalida cada operação no banco. Recursos comerciais acrescentados nas fases seguintes devem reutilizar essa fronteira e incluir OrganizationId nas consultas e vínculos.
