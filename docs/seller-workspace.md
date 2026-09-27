# Workspace do vendedor

A área autenticada organiza o trabalho por organização e mantém a organização ativa na URL.

- `/dashboard`: totais reais de produtos, ofertas e clientes, checklist de publicação e estado explícito de pagamentos.
- `/products` e `/offers`: listas e editores existentes, incluindo deep links para criação e edição.
- `/customers`: diretório paginado de clientes identificados nos checkouts daquela organização.
- `/team`: membros e papéis, respeitando as permissões persistidas.
- `/settings`: nome da organização e papel do usuário.

Receita permanece como indisponível enquanto não houver pagamentos reais. O dashboard não fabrica vendas, conversão, saldo ou gráficos. A timeline de vendas e métricas financeiras continuam dependentes das fases de pagamentos, ledger e vendas.

Em telas menores, a navegação numerada vira uma faixa horizontal e tabelas passam para linhas empilhadas. Todos os destinos continuam acessíveis por teclado e URLs diretas.

