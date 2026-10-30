import { Alert } from '../../components/Alert';
import { useStock } from './hooks';

export function StockPanel() {
  const stock = useStock();

  return (
    <aside className="card">
      <h2>Stock</h2>
      {stock.isError && <Alert kind="error">Inventario no responde: {stock.error.message}</Alert>}
      {stock.isPending ? (
        <p className="muted">Cargando…</p>
      ) : (
        <ul className="stock-list">
          {stock.data?.map((s) => (
            <li key={s.sku}>
              <code>{s.sku}</code>
              <span className={s.disponible === 0 ? 'stock-out' : ''}>{s.disponible}</span>
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}
