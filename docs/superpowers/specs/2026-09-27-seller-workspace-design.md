# Seller workspace design

## Objective

Turn the authenticated area into a coherent seller workspace while keeping every number and status grounded in implemented APIs. The first screen should tell a seller what exists, what remains to publish, and where to act next without suggesting that payments or revenue are available.

## Direction

Use the existing Swiss visual system: white surfaces, Helvetica, Yves Klein Blue, visible hairlines, left alignment, and asymmetrical grids. The signature move is a numbered workspace index: navigation sections and onboarding steps share large tabular numerals, making progress and location visible without decorative cards or fake charts.

## Information architecture

The authenticated shell has one global header and one organization workspace. A selected organization exposes these destinations:

- Visão geral: real counts for products, offers, and customers; payment state shown as unavailable; publication checklist.
- Produtos and Ofertas: existing catalog lists and editors.
- Clientes: real paginated customer directory with contact data already authorized for organization members.
- Equipe: existing membership management.
- Configurações: organization naming and current role.

Organization selection remains explicit and is stored in the URL. Creating an organization lands on its overview. Existing product and offer deep links remain valid.

## Behavior and truthfulness

Overview counts come from the first page response's `total`, never from fabricated values. The publication checklist marks product and offer creation complete from those totals. Publishing remains an explicit action because an exact all-pages active-offer query does not yet exist. Revenue and payments display an unavailable state and explain the provider dependency.

Customers are read-only in this release. The list is tenant-scoped by the existing endpoint and includes pagination. Empty and error states provide a direct next action. No sales timeline or totals appear.

## Responsive behavior

Desktop uses a narrow indexed navigation rail and a fluid content canvas. Below 900 px the rail becomes a horizontally scrollable section index; content remains a single column. Controls retain visible labels and keyboard focus.

## Validation

Component tests cover route parsing, overview truthfulness, navigation, customer empty/data states, and organization creation landing. Existing catalog, organization, authentication, and public checkout tests must continue to pass. Build and lint are required, followed by browser review at desktop and mobile widths.

