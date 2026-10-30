export interface OrderFormValues {
  clienteNombre: string;
  sku: string;
  cantidad: string; // texto del input; se convierte al enviar
}

export type FieldErrors = Partial<Record<keyof OrderFormValues, string>>;

export const MIN_CANTIDAD = 1;
export const MAX_CANTIDAD = 100;

/**
 * Mismas reglas que el backend, para dar feedback inmediato sin ir al servidor.
 * El backend sigue siendo la fuente de verdad: si algo se escapa aquí, su 400 se muestra igual.
 */
export function validateOrderForm(values: OrderFormValues): FieldErrors {
  const errors: FieldErrors = {};

  if (!values.clienteNombre.trim()) errors.clienteNombre = 'El nombre del cliente es obligatorio.';
  else if (values.clienteNombre.trim().length > 120) errors.clienteNombre = 'Máximo 120 caracteres.';

  if (!values.sku) errors.sku = 'Selecciona un producto.';

  const raw = values.cantidad.trim();
  const cantidad = Number(raw);
  if (!raw) errors.cantidad = 'La cantidad es obligatoria.';
  else if (!Number.isInteger(cantidad)) errors.cantidad = 'La cantidad debe ser un número entero.';
  else if (cantidad < MIN_CANTIDAD || cantidad > MAX_CANTIDAD)
    errors.cantidad = `La cantidad debe estar entre ${MIN_CANTIDAD} y ${MAX_CANTIDAD}.`;

  return errors;
}
