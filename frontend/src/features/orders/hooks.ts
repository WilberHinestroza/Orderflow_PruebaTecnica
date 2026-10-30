import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { inventoryApi, ordersApi, type CreateOrderInput, type Order } from '../../api/orders';

/**
 * Estado del servidor con React Query: cache, reintentos, loading/error y polling sin código manual.
 * El estado local de UI (inputs del formulario, pestaña activa) vive en useState de cada componente.
 */
export const queryKeys = {
  orders: ['orders'] as const,
  products: ['products'] as const,
  stock: ['stock'] as const,
};

const FAST_POLL_MS = 1500; // mientras haya pedidos Pending
const SLOW_POLL_MS = 5000; // cuando todo está en estado final

/** Polling adaptativo: rápido mientras algo está Pending, lento cuando no hay nada que esperar. */
export function useOrders() {
  return useQuery({
    queryKey: queryKeys.orders,
    queryFn: ordersApi.list,
    refetchInterval: (query) => {
      const orders = query.state.data as Order[] | undefined;
      return orders?.some((o) => o.estado === 'Pending') ? FAST_POLL_MS : SLOW_POLL_MS;
    },
  });
}

export function useProducts() {
  return useQuery({ queryKey: queryKeys.products, queryFn: ordersApi.products, staleTime: Infinity });
}

export function useStock() {
  return useQuery({ queryKey: queryKeys.stock, queryFn: inventoryApi.stock, refetchInterval: 3000 });
}

export function useCreateOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateOrderInput) => ordersApi.create(input),
    onSuccess: (created) => {
      // Lo agregamos de inmediato a la lista (sin esperar al siguiente poll) y refrescamos.
      queryClient.setQueryData<Order[]>(queryKeys.orders, (prev) => [created, ...(prev ?? [])]);
      void queryClient.invalidateQueries({ queryKey: queryKeys.orders });
    },
  });
}

export function useRepublishOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => ordersApi.republish(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.stock }),
  });
}
