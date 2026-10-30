import { validateOrderForm } from './validation';

describe('validateOrderForm', () => {
  it('acepta un pedido válido', () => {
    expect(validateOrderForm({ clienteNombre: 'Ana', sku: 'ABC-01', cantidad: '3' })).toEqual({});
  });

  it('reporta cada campo inválido por separado', () => {
    const errors = validateOrderForm({ clienteNombre: '   ', sku: '', cantidad: '101' });
    expect(Object.keys(errors).sort()).toEqual(['cantidad', 'clienteNombre', 'sku']);
  });

  it.each(['0', '1.5', '', 'abc'])('rechaza la cantidad "%s"', (cantidad) => {
    expect(validateOrderForm({ clienteNombre: 'Ana', sku: 'ABC-01', cantidad }).cantidad).toBeDefined();
  });
});
