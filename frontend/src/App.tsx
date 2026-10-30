import { useState } from 'react';
import { OrderForm } from './features/orders/OrderForm';
import { OrderList } from './features/orders/OrderList';
import { StockPanel } from './features/orders/StockPanel';

type View = 'create' | 'list';

export function App() {
  const [view, setView] = useState<View>('create');

  return (
    <div className="app">
      <header className="topbar">
        <h1>OrderFlow</h1>
        <nav className="tabs" role="tablist">
          <button role="tab" aria-selected={view === 'create'} onClick={() => setView('create')}>
            Nuevo pedido
          </button>
          <button role="tab" aria-selected={view === 'list'} onClick={() => setView('list')}>
            Pedidos
          </button>
        </nav>
      </header>

      <main className="layout">
        {view === 'create' ? <OrderForm /> : <OrderList />}
        <StockPanel />
      </main>
    </div>
  );
}
