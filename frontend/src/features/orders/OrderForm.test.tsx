import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { OrderForm } from './OrderForm';

function jsonResponse(status: number, body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }));
}

function renderForm() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <OrderForm />
    </QueryClientProvider>,
  );
}

afterEach(() => vi.restoreAllMocks());

describe('OrderForm', () => {
  it('muestra en pantalla el error de validación que devuelve el servidor junto al campo', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input);
      if (url.endsWith('/api/products')) return jsonResponse(200, [{ sku: 'ABC-01', nombre: 'Teclado' }]);
      return jsonResponse(400, {
        title: 'Los datos del pedido no son válidos.',
        errors: { sku: ["El SKU 'ABC-01' no existe en el catálogo."] },
      });
    });

    renderForm();
    await screen.findByRole('option', { name: /ABC-01/ });

    fireEvent.change(screen.getByLabelText('Cliente'), { target: { value: 'Ana' } });
    fireEvent.change(screen.getByLabelText('Producto'), { target: { value: 'ABC-01' } });
    fireEvent.click(screen.getByRole('button', { name: 'Crear pedido' }));

    expect(await screen.findByText("El SKU 'ABC-01' no existe en el catálogo.")).toBeTruthy();
  });

  it('valida en el cliente sin llamar al servidor', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(() => jsonResponse(200, []));

    renderForm();
    fireEvent.click(screen.getByRole('button', { name: 'Crear pedido' }));

    expect(await screen.findByText('El nombre del cliente es obligatorio.')).toBeTruthy();
    expect(fetchSpy).not.toHaveBeenCalledWith('/api/orders', expect.anything());
  });
});
