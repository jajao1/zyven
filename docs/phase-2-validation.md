# Validação da fase 2

Concluída em 27/09/2026 sobre os commits c704f9f e 3ffb5e8.

- Gate independente `scripts/test.ps1`: 5 testes unitários, 20 de integração PostgreSQL/Redis e 16 de frontend aprovados; builds, formatação, lint e ausência de divergência EF aprovados.
- Migrations aplicadas em banco efêmero do zero e na instalação Docker existente. Containers API, PostgreSQL e Redis saudáveis; Worker iniciado.
- Rebuild Compose com .NET 10 estável e smoke completo pelo proxy: cadastro, perfil, refresh, logout, rejeição de acesso revogado e novo login.
- Navegador: criação de organização, inclusão de usuário de teste, rejeição do rebaixamento do último OWNER, entrada de OPERATOR com equipe somente para leitura. Layout móvel 390×844 sem overflow horizontal e desktop 1280×900 conferidos.
- Revisão de especificação aprovada. Revisão de qualidade identificou resposta de sessão atrasada após logout e cache entre contas; ambos corrigidos e cobertos por regressões determinísticas, com nova revisão sem bloqueadores.
- Isolamento bidirecional entre organizações e proteção concorrente do último OWNER exercitados com PostgreSQL real.

Não há convites por e-mail nem dados comerciais nesta fase. A inclusão na equipe usa contas já cadastradas. A fase 3 começa somente após este fechamento.

CI Linux também aprovada sobre 1f1a1fc: https://github.com/jajao1/zyven/actions/runs/36339588341 (backend e frontend).
