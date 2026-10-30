# OrderFlow

Sistema de pedidos con reserva de inventario asíncrona. Tiene dos servicios .NET 8 que se comunican por RabbitMQ y un panel en React.

```
             ┌──────────────┐  POST /orders   ┌───────────────────┐
  Operador → │ Panel (React)│ ──────────────→ │    Orders API     │──┐ pedido + evento en
             │  nginx :8080 │ ←─ polling ──── │  (.NET 8) :5080   │  │ la MISMA transacción
             └──────────────┘                 └───────────────────┘  │
                                                ▲        │ outbox    ▼
                                StockReserved / │        │      ┌──────────┐
                                StockRejected   │        ▼      │ orders DB│
                                          ┌──────────────────┐  └──────────┘
                                          │     RabbitMQ     │
                                          │ orderflow.events │
                                          └──────────────────┘
                                                ▲        │ OrderCreated
                                         outbox │        ▼
                                          ┌───────────────────┐   ┌─────────────┐
                                          │ Inventory Worker  │──→│ inventory DB│
                                          │  (.NET 8) :5081   │   └─────────────┘
                                          └───────────────────┘
```

---

## 1. Levantar todo (menos de 10 minutos)

**Requisito:** Docker Desktop (o Docker Engine con Compose v2).

```bash
git clone <repo> && cd orderflow
docker compose up --build
```

La primera vez tarda unos minutos porque descarga imágenes y compila. Cuando los servicios pasen a `healthy`, abre:

| Qué | URL |
|---|---|
| **Panel de pedidos** | http://localhost:8080 |
| Swagger de Orders API | http://localhost:5080/swagger |
| Stock (Inventory) | http://localhost:5081/stock |
| Consola de RabbitMQ | http://localhost:15672 (usuario `orderflow`, contraseña `orderflow`) |

Las credenciales tienen valores por defecto solo para desarrollo local. Para cambiarlas, copia `.env.example` a `.env`.

Para apagar todo: `docker compose down`. Para empezar de cero, borrando también los datos: `docker compose down -v`.

### Productos del seed

Se cargan automáticamente al arrancar.

| SKU | Producto | Stock inicial | Para qué sirve en la demo |
|---|---|---|---|
| ABC-01 | Teclado mecánico | 100 | Pedidos que se confirman |
| DEF-02 | Mouse inalámbrico | 50 | Pedidos que se confirman |
| XYZ-03 | Monitor 27" | 5 | Se agota rápido: ver un `Rejected` |
| GHI-04 | Audífonos | 0 | Siempre `Rejected` |

## 2. Cómo probar los criterios de aceptación

1. **Pedido que se confirma.** En *Nuevo pedido* crea `Ana / ABC-01 / 3`. En *Pedidos* aparece como **Pendiente** y en uno o dos segundos pasa a **Confirmado**. El panel de stock baja de 100 a 97.
2. **Pedido que se rechaza.** Crea un pedido de `GHI-04`, o uno de `XYZ-03` con cantidad 10. Pasa a **Rechazado** y muestra el motivo.
3. **Errores de validación.** Deja el nombre vacío o pon cantidad 0 o 150. El error aparece en rojo junto al campo. Si el servidor rechaza algo, su mensaje 400 también se muestra junto al campo correspondiente.
4. **Idempotencia.** En *Pedidos*, pulsa **Reenviar evento** en un pedido confirmado. Esto vuelve a publicar el mismo `OrderCreated`, con el mismo `EventId`. El stock **no cambia**, y en los logs de Inventory (`docker compose logs inventory-worker`) aparece `ya procesado; se ignora`. Este endpoint de demo (`POST /orders/{id}/republish`) se desactiva con `DEMO_ENDPOINTS=false`. El test automatizado `El_mismo_OrderCreated_procesado_dos_veces_descuenta_el_stock_una_sola_vez` cubre el mismo caso.
5. **Broker caído.**
   - `docker compose stop rabbitmq` y crea un pedido: la API responde `201` y el pedido queda **Pendiente**.
   - `docker compose start rabbitmq`: en unos segundos el outbox publica y el pedido se confirma.
6. **Inventory caído.**
   - `docker compose stop inventory-worker` y crea un pedido: queda **Pendiente** y el mensaje espera en la cola `inventory.order-created` (visible en la consola de RabbitMQ).
   - `docker compose start inventory-worker`: lo procesa y el pedido se resuelve.

## 3. Tests

Tienes dos opciones, ambas con un solo comando:

```bash
# Con el SDK de .NET 8 instalado
dotnet test

# Sin SDK, dentro de un contenedor
docker compose --profile test run --rm tests

# Frontend (opcional)
cd frontend && npm install && npm test
```

**Backend (xUnit).** Usa SQLite en memoria, que es una base de datos relacional real: respeta transacciones, llaves primarias y CHECK constraints. El provider InMemory de EF no las respeta y no serviría para probar la idempotencia.

| Test | Qué demuestra |
|---|---|
| `El_mismo_OrderCreated_procesado_dos_veces_descuenta_el_stock_una_sola_vez` | Idempotencia del consumidor: el stock se descuenta una sola vez, queda un solo `processed_event` y un solo `StockReserved` |
| `Sin_stock_suficiente_rechaza_y_no_toca_el_stock` | Ruta de rechazo: el stock queda intacto y se emite `StockRejected` |
| `Pending_pasa_a_Confirmed_y_los_eventos_repetidos_o_contradictorios_no_lo_cambian` | Máquina de estados: resultados duplicados o tardíos no corrompen el pedido |
| `Validacion_reporta_el_campo_invalido` (6 casos) | Reglas de validación: nombre vacío, SKU inexistente, cantidad fuera de 1–100 |
| `Pedido_valido_se_guarda_Pending_junto_con_su_evento_en_el_outbox` | El pedido y su evento se guardan juntos; un pedido inválido no persiste nada |

**Frontend (Vitest + Testing Library).** 8 tests sobre la validación del formulario y sobre cómo se muestran en pantalla los errores 400 del servidor.

## 4. Estructura

```
src/
  OrderFlow.BuildingBlocks/     Contratos de eventos, conexión RabbitMQ, consumidor base, outbox
  OrderFlow.Orders.Api/         Minimal API: Domain/ Data/ Features/ Messaging/
  OrderFlow.Inventory.Worker/   Worker + /health y /stock: Domain/ Data/ Features/ Messaging/
tests/OrderFlow.Tests/          xUnit
frontend/                       React + Vite + TypeScript + React Query, servido por nginx
k8s/                            Manifiestos de Kubernetes (bonus)
docker-compose.yml
```

Dentro de cada servicio el código está organizado por responsabilidad:

- `Domain`: entidades y reglas, como la máquina de estados del pedido.
- `Features`: casos de uso testeables sin infraestructura, como `CreateOrderHandler`, `ReserveStockHandler` y `StockResultHandler`.
- `Messaging`: adaptadores finos que traducen un mensaje de RabbitMQ a una llamada al handler.

## 5. Decisiones de arquitectura

### Mensajería: RabbitMQ con un exchange topic

Hay un exchange `orderflow.events` y cada evento lleva su routing key (`order.created`, `stock.reserved`, `stock.rejected`). Cada servicio tiene su propia cola durable: `inventory.order-created` y `orders.stock-results`. Ambas tienen una dead-letter queue (`*.dlq`) para mensajes imposibles de procesar.

Declarar la topología es idempotente y la hacen los dos servicios, así que no importa cuál arranque primero.

Usé `RabbitMQ.Client` directamente en lugar de MassTransit. La prueba evalúa justo la mensajería, y quería que el ack manual, la DLQ, los publisher confirms y el outbox fueran visibles y explicables, no escondidos detrás de un framework. En producción probablemente usaría MassTransit, que trae outbox, retries e idempotencia, sabiendo que su versión 9 tiene licencia comercial.

### Transactional Outbox, en ambos servicios

Guardar en la base de datos y publicar en RabbitMQ no pueden ser atómicos entre sí. Por eso el evento se guarda como una fila en `outbox_messages`, en la **misma transacción** que el cambio de negocio. Después, un `BackgroundService` (`OutboxPublisher`) publica las filas pendientes cada segundo.

- Usa **publisher confirms**: una fila solo se marca como publicada cuando el broker confirma que la recibió.
- Usa `mandatory: true`: un mensaje que ninguna cola recibe produce un error, no se pierde en silencio.
- La garantía resultante es **entrega al menos una vez**. Por eso los consumidores son idempotentes.

### Idempotencia del consumidor de Inventory

`ReserveStockHandler` hace, en una sola transacción:

1. `UPDATE stock SET Disponible = Disponible - N WHERE Sku = X AND Disponible >= N`. Es atómico en la base de datos: no hay "leer, restar y guardar", así que dos pedidos simultáneos nunca dejan el stock negativo. Además hay un `CHECK (Disponible >= 0)` como red de seguridad.
2. `INSERT processed_events(EventId)`. La llave primaria es el `EventId`.
3. `INSERT outbox_messages` con el resultado (`StockReserved` o `StockRejected`).

Si llega un duplicado, una consulta previa lo detecta y lo descarta sin tocar nada. Si llegan dos copias **simultáneas** (por ejemplo, en dos réplicas), ambas pasan la consulta previa. Sin embargo, la segunda choca con la llave primaria en el paso 2, la transacción hace rollback y su `UPDATE` se deshace. La garantía la da la base de datos, no el código.

### Idempotencia del consumidor de Orders

No necesita tabla extra. La máquina de estados (`Order.Confirm` / `Order.Reject`) solo permite pasar de `Pending` a un estado final. Un resultado repetido devuelve `AlreadyApplied` y uno contradictorio devuelve `Invalid`. En ningún caso se guarda nada.

### Persistencia: PostgreSQL con una base por servicio

Una sola instancia de Postgres en compose, pero con dos bases separadas (`orders` e `inventory`), cada una propiedad de su servicio. Ningún servicio lee la base del otro.

**Por qué PostgreSQL y no SQLite o memoria:** la idempotencia y el descuento de stock dependen de garantías de la base de datos (transacciones, llave primaria en `processed_events`, `UPDATE` condicional atómico y `CHECK`), y con dos servicios concurrentes eso necesita un motor real compartible por red. Además los pedidos y el stock persisten entre reinicios gracias al volumen de Docker. SQLite se usa solo en los tests, donde basta un proceso.

El esquema se crea con `EnsureCreated` al arrancar, con reintentos mientras la base de datos levanta, y el seed es idempotente.

### Catálogo duplicado en Orders

La validación "SKU existente en el catálogo" se hace en Orders contra su propia tabla `products`. Así no hay una llamada HTTP síncrona a Inventory, que acoplaría la disponibilidad de los dos servicios. El stock, en cambio, solo lo conoce Inventory.

### Frontend: React Query para el estado del servidor

React Query maneja el cache, los reintentos y los estados de carga y error. El polling es **adaptativo**: cada 1,5 segundos mientras algún pedido está `Pending` y cada 5 segundos cuando no.

El estado local de la interfaz (los inputs y la pestaña activa) vive en `useState`.

La validación del formulario replica la del backend para dar feedback inmediato, pero el backend sigue siendo la fuente de verdad: sus 400 se muestran junto a cada campo. Si un refresco falla, el panel avisa pero sigue mostrando los últimos datos conocidos.

nginx sirve el build y hace de proxy de `/api` y `/inventory`, así que no hay CORS que configurar.

### Configuración

Toda la configuración llega por variables de entorno: `ConnectionStrings__OrdersDb`, `RabbitMq__Host`, `RabbitMq__Password`, etc.

- Las credenciales de RabbitMQ no tienen valor por defecto en el código. Se validan al arrancar (`ValidateOnStart`) y, si faltan, el servicio no inicia.
- Los valores por defecto de desarrollo están solo en `docker-compose.yml`, para cumplir con "un solo comando, sin pasos manuales".

## 6. Manejo de fallos

| Escenario | Qué pasa |
|---|---|
| **Broker caído cuando Orders crea un pedido** | El pedido y el evento se guardan igual y la API responde `201`. El `OutboxPublisher` registra el error (`Attempts`, `LastError`) y reintenta cada segundo. Cuando RabbitMQ vuelve, publica en orden. El pedido se queda en `Pending` mientras tanto. Nada se pierde. |
| **Inventory caído o lento** | `OrderCreated` espera en una cola durable con mensajes persistentes. El pedido sigue en `Pending` hasta que Inventory vuelve y lo procesa. No hay timeout que lo rechace (ver trade-offs). |
| **Inventory muere a mitad de procesar** | El ack es manual y se envía **después** de hacer commit. Si muere antes, RabbitMQ reentrega el mensaje. Si alcanzó a hacer commit, la reentrega se detecta como duplicado. |
| **Base de datos caída mientras se consume** | El handler falla, se espera 2 segundos y se hace `nack` con requeue. El mensaje se reintenta hasta que la base de datos vuelve. |
| **Mensaje corrupto o desconocido** | `PoisonMessageException` provoca un `nack` sin requeue y el mensaje termina en la `*.dlq` para revisarlo. No bloquea la cola. |
| **Broker se reinicia con los servicios arriba** | `AutomaticRecoveryEnabled` reconecta la conexión, los canales y las suscripciones. Las colas y los mensajes persistentes sobreviven al reinicio. |
| **Base de datos o broker no listos al arrancar** | Compose espera sus healthchecks. Además, el código reintenta la inicialización de la base de datos y la conexión al broker. |

Los healthchecks están separados en dos:

- `/health/live` solo indica si el proceso está vivo.
- `/health/ready` verifica la base de datos. En Orders, a propósito, **no** depende de RabbitMQ: gracias al outbox, la API puede seguir aceptando pedidos con el broker caído.

## 7. Trade-offs asumidos

- **Un pedido puede quedar `Pending` indefinidamente** si Inventory nunca vuelve. Rechazarlo por timeout sin coordinarse con Inventory podría dejar un pedido `Rejected` con stock ya descontado. La solución correcta (estado `Expired` más un evento de compensación `ReleaseStock`) está en "Qué haría distinto".
- **Reintentos sin límite** para errores transitorios del consumidor: se reencola con una espera fija de 2 segundos. Es simple y suficiente aquí. Lo ideal es contar los intentos y aplicar backoff exponencial (ver abajo).
- **`EnsureCreated` en vez de migraciones de EF**: más simple de levantar, pero no permite evolucionar el esquema. En producción usaría migraciones, aplicadas en un Job antes del despliegue.
- **Librería compartida (`BuildingBlocks`)**: acopla ligeramente los servicios en tiempo de compilación. Es aceptable en un monorepo; en un sistema real los contratos se publicarían como paquete NuGet versionado.
- **Polling en lugar de SignalR**: el enunciado lo acepta, y el polling adaptativo da una buena experiencia con muy poco código.
- **El outbox se publica con un solo publicador por servicio**: con varias réplicas, dos podrían tomar la misma fila y publicarla dos veces. Eso es seguro gracias a la idempotencia, pero ineficiente. Se resolvería con `SELECT ... FOR UPDATE SKIP LOCKED`.
- **.NET 8 LTS**: su soporte termina en noviembre de 2026. Migrar a .NET 10 LTS es cambiar el `TargetFramework` y las versiones de los paquetes.

## 8. Kubernetes (bonus)

`k8s/` contiene los manifiestos de `orders-api` e `inventory-worker`: Namespace, ConfigMap, Deployment y Service, más un ejemplo de Secret y un `kustomization.yaml`.

- **Probes:** liveness en `/health/live` y readiness en `/health/ready`, con los criterios explicados arriba.
- **Recursos:** requests de `100m / 128Mi` y limits de `500m / 256Mi`.
- **Seguridad:** se ejecuta sin root, con `readOnlyRootFilesystem` (más un `emptyDir` en `/tmp`) y sin escalamiento de privilegios.
- **Despliegue:** `RollingUpdate` con `maxUnavailable: 0`.
- **Secretos:** las credenciales salen de un Secret, nunca del ConfigMap.

Cómo desplegaría el sistema completo:

1. **Imágenes:** CI construye las imágenes con los mismos Dockerfiles y las publica en un registry (GHCR o ACR) con un tag inmutable, el SHA del commit.
2. **Datos:** PostgreSQL gestionado (Azure Database for PostgreSQL o RDS) en lugar de dentro del clúster. RabbitMQ con el *RabbitMQ Cluster Operator*, con 3 nodos y colas quorum.
3. **Secretos:** External Secrets Operator, que lee desde Key Vault o Vault y crea el Secret `orderflow-secrets`.
4. **Esquema:** un `Job` de migraciones de EF que corre antes de cada despliegue, por ejemplo con un hook de Helm o Argo CD.
5. **Frontend:** un Deployment de nginx más un `Ingress` que enruta `/` al panel y `/api` a `orders-api`, con TLS mediante cert-manager.
6. **Escalado:** HPA para `orders-api` por CPU y KEDA para `inventory-worker` según la longitud de la cola de RabbitMQ.

Para aplicarlo: `kubectl apply -f k8s/secrets.example.yaml` (con valores reales) y luego `kubectl apply -k k8s/`.

## 9. Qué haría distinto con más tiempo

- **Expiración de pedidos y compensación:** un estado `Expired` tras N minutos en `Pending`, un evento `OrderExpired` e Inventory devolviendo el stock si ya lo había reservado. Es el inicio de una saga coreografiada.
- **Reintentos con backoff y límite:** colas de retry con TTL (*delayed retry*) y, tras N intentos, envío a la DLQ. Hoy un error transitorio persistente se reintenta indefinidamente.
- **Tests de integración** con Testcontainers (Postgres y RabbitMQ reales) que recorran el flujo completo, incluido el caso del broker caído.
- **Migraciones de EF** versionadas en lugar de `EnsureCreated`.
- **Observabilidad:** OpenTelemetry con trazas que crucen HTTP, RabbitMQ y el consumidor (propagando `traceparent` en los headers del mensaje), métricas de la longitud del outbox y de la cola, y logs estructurados.
- **SignalR** para empujar los cambios de estado al panel en lugar de hacer polling.
- **Limpieza** de `outbox_messages` publicados y de `processed_events` antiguos con un job periódico.
- **Concurrencia optimista** en `Order` (columna `xmin` de Postgres) por si en el futuro varios procesos actualizan el mismo pedido.
- **Paginación y filtros** en `GET /orders`, que hoy devuelve los 100 más recientes.

## 10. Correr sin Docker (desarrollo)

Necesitas Postgres y RabbitMQ accesibles; puedes levantar solo esos dos con `docker compose up postgres rabbitmq` y luego:

```bash
# Terminal 1 (PowerShell: $env:VAR="valor")
export ConnectionStrings__OrdersDb="Host=localhost;Port=5432;Database=orders;Username=orderflow;Password=orderflow"
export RabbitMq__Host=localhost RabbitMq__Username=orderflow RabbitMq__Password=orderflow Features__DemoEndpoints=true
dotnet run --project src/OrderFlow.Orders.Api --urls http://localhost:5080

# Terminal 2
export ConnectionStrings__InventoryDb="Host=localhost;Port=5432;Database=inventory;Username=orderflow;Password=orderflow"
export RabbitMq__Host=localhost RabbitMq__Username=orderflow RabbitMq__Password=orderflow
dotnet run --project src/OrderFlow.Inventory.Worker --urls http://localhost:5081

# Terminal 3
cd frontend && npm install && npm run dev
```

Para que esto funcione, Postgres debe estar expuesto en el puerto 5432. Agrega `ports: ["5432:5432"]` al servicio `postgres` del compose, o usa una instancia local.
