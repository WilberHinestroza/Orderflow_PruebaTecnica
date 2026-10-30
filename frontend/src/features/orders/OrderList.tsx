import { Alert } from '../../components/Alert';
import { StatusBadge } from '../../components/StatusBadge';
import { useOrders, useRepublishOrder } from './hooks';

const dateFormat = new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'medium' });

export function OrderList() {
  const orders = useOrders();
  const republish = useRepublishOrder();

  return (
    <section className="card">
      <header className="card-header">
        <h2>Pedidos</h2>
        <span className="muted" aria-live="polite">
          {orders.isFetching ? 'Actualizando…' : orders.dataUpdatedAt ? `Actualizado ${dateFormat.format(orders.dataUpdatedAt)}` : ''}
        </span>
      </header>

      {/* Si un refresco falla, se avisa pero se siguen mostrando los últimos datos conocidos. */}
      {orders.isError && <Alert kind="error">No se pudo actualizar la lista: {orders.error.message}</Alert>}
      {republish.isSuccess && (
        <Alert kind="info">
          Evento reenviado con el mismo EventId. El stock no debería cambiar: Inventario lo detecta como duplicado.
        </Alert>
      )}
      {republish.isError && <Alert kind="error">No se pudo reenviar el evento: {republish.error.message}</Alert>}

      {orders.isPending ? (
        <p className="muted">Cargando pedidos…</p>
      ) : !orders.data?.length ? (
        <p className="muted">Todavía no hay pedidos. Crea el primero en la pestaña Nuevo pedido.</p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Cliente</th>
                <th>SKU</th>
                <th className="num">Cant.</th>
                <th>Estado</th>
                <th>Creado</th>
                <th aria-label="Acciones" />
              </tr>
            </thead>
            <tbody>
              {orders.data.map((o) => (
                <tr key={o.id}>
                  <td>{o.clienteNombre}</td>
                  <td>
                    <code>{o.sku}</code>
                  </td>
                  <td className="num">{o.cantidad}</td>
                  <td>
                    <StatusBadge status={o.estado} />
                    {o.motivoRechazo && <div className="muted small">{o.motivoRechazo}</div>}
                  </td>
                  <td className="small">{dateFormat.format(new Date(o.creadoEn))}</td>
                  <td>
                    <button
                      type="button"
                      className="link"
                      onClick={() => republish.mutate(o.id)}
                      disabled={republish.isPending}
                      title="Vuelve a publicar el mismo OrderCreated para comprobar que el stock no se descuenta dos veces"
                    >
                      Reenviar evento
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
