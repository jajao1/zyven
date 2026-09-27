# Evidências de conclusão da FASE 1

Validação em 27/09/2026 após revisão de especificação e revisão de qualidade/segurança.

- `scripts/test.ps1`: exit 0, banco PostgreSQL e Redis novos e isolados.
- Build .NET: zero erros e avisos.
- 5 testes unitários e 11 testes de integração aprovados.
- 7 testes de frontend aprovados; build Vite/TypeScript e lint aprovados.
- `dotnet format --verify-no-changes`: aprovado.
- Duas migrations aplicadas no banco novo; modelo EF sem alterações pendentes.
- `docker compose up -d --build`: API, frontend, PostgreSQL e Redis saudáveis; Worker ativo e registrado no Hangfire; migrador terminou com sucesso.
- `scripts/smoke.ps1`: cadastro, perfil, refresh, logout, revogação de access/refresh e novo login aprovados pelo proxy.
- Navegador: cadastro, recarregamento mantendo sessão, logout e login; layout de 390×844 inspecionado.
- Auditoria NuGet transitiva e npm sem vulnerabilidades reportadas no momento da execução.

O teste determinístico de concorrência reproduziu HTTP 500 quando a limpeza apagava uma sessão enquanto o refresh aguardava seu bloqueio. A correção retorna 401 nesse caso; o teste passou no banco real. Replay de refresh, logout concorrente, expiração absoluta, duplicação de e-mail e senha incorreta também estão cobertos.

O SDK local disponível era 10.0.400 preview; o build Docker foi validado separadamente com a imagem estável SDK 10.0 e runtime ASP.NET 10.0. O aviso npm sobre a configuração global `python` é externo ao repositório e não impediu os checks. O servidor Vite deve estar encerrado no Windows durante `npm ci` para liberar arquivos nativos.

Esta conclusão cobre a FASE 1. Dados comerciais, tenant isolation e processamento financeiro serão adicionados nas fases correspondentes.
