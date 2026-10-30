import { request } from './http';

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected';

export interface Order {
  id: string;
  clienteNombre: string;
  sku: string;
  cantidad: number;
  estado: OrderStatus;
  creadoEn: string;
  actualizadoEn: string | null;
  motivoRechazo: string | null;
}

export interface Product {
  sku: string;
  nombre: string;
}

export interface StockItem {
  sku: string;
  disponible: number;
}

export interface CreateOrderInput {
  clienteNombre: string;
  sku: string;
  cantidad: number;
}

export const ordersApi = {
  list: () => request<Order[]>('/api/orders'),
  create: (input: CreateOrderInput) =>
    request<Order>('/api/orders', { method: 'POST', body: JSON.stringify(input) }),
  products: () => request<Product[]>('/api/products'),
  republish: (id: string) => request<{ eventId: string }>(`/api/orders/${id}/republish`, { method: 'POST' }),
};

export const inventoryApi = {
  stock: () => request<StockItem[]>('/inventory/stock'),
};
