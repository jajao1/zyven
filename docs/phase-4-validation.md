# Validação da fase 4

Concluída em 27/09/2026. Implementação: 4ff85a7, e707c05, 57b6cae; ajuste de título público em c519c3c.

- Gate completo: 32 integrações reais, 7 unitários e 26 frontend; builds, lint, formatação, migrations e modelo sem divergências.
- O ajuste final do título público acrescentou regressão RED/GREEN e passou 27 testes frontend, build e lint. Total atual: 66 testes.
- Revisão de especificação COMPLIANT e qualidade/segurança sem bloqueadores. Corrigidos com regressões: editor reabrindo conteúdo antigo e recibo apresentado junto da oferta errada.
- Compose reconstruído e migration aplicada sobre os dados das fases anteriores. API/frontend/PostgreSQL/Redis saudáveis, Worker ativo.
- Smoke pelo proxy: publicação somente após ativação, conteúdo/valor no servidor, preço adulterado pelo comprador ignorado, origem externa rejeitada, body acima de 128 KiB recebe 413, cookie protegido e consulta sem cookie recebe 404.
- Navegador: operador editou conteúdo, ativou produto, abriu página pública, criou checkout anônimo de 59.90 e retomou a sessão após reload. Layout desktop e móvel 390×844 conferidos; conteúdo móvel 375px sem overflow horizontal.
- Expiração é validada no acesso e materializada por Hangfire a cada cinco minutos; teste real confirma estado EXPIRED. Testes também cobrem papéis/tenant, mídia inválida, campos obrigatórios e limite de taxa público.

O smoke inicial reutilizou a sessão HTTP da origem inválida, mantendo esse cabeçalho em requests seguintes; o script foi corrigido para usar sessões independentes. O teste completo posterior passou. Nenhum dado do comprador é acessível apenas pelo ID na URL.

Não há pagamento simulado nem confirmação de venda nesta fase. CustomerId será associado na fase 5 preservando checkouts existentes.
