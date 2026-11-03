# SPEC-003 — Cart, Quote, Checkout Saga, Order, PSP Fake

**Milestone:** M3 · **Status:** DRAFT · **Repo:** atlas-storefront-dotnet (Cart, Checkout, Orders, Payments services).

Jornada: carrinho → quote congelada com TTL → SubmitCheckout idempotente → reserva autoritativa → pedido PENDING_PAYMENT com snapshot → PSP fake determinístico → confirmação/compensação → projeções + saga resiliente a restart/replay.
