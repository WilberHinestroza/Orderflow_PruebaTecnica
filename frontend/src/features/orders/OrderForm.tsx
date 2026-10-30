import { useState, type FormEvent, type ReactNode } from 'react';
import { ApiError } from '../../api/http';
import type { Order } from '../../api/orders';
import { Alert } from '../../components/Alert';
import { useCreateOrder, useProducts } from './hooks';
import { MAX_CANTIDAD, MIN_CANTIDAD, validateOrderForm, type FieldErrors, type OrderFormValues } from './validation';

const EMPTY: OrderFormValues = { clienteNombre: '', sku: '', cantidad: '1' };

interface OrderFormProps {
  onCreated?: (order: Order) => void;
}

export function OrderForm({ onCreated }: OrderFormProps) {
  const [values, setValues] = useState<OrderFormValues>(EMPTY);
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});
  const [lastCreated, setLastCreated] = useState<Order | null>(null);

  const products = useProducts();
  const createOrder = useCreateOrder();

  // Errores del servidor (400) por campo; se combinan con los del cliente.
  const serverError = createOrder.error instanceof ApiError ? createOrder.error : null;
  const fieldError = (field: keyof OrderFormValues) =>
    clientErrors[field] ?? serverError?.fieldErrors[field]?.[0];
  const generalError = createOrder.error && !hasFieldErrors(serverError) ? createOrder.error.message : null;

  const update = (field: keyof OrderFormValues, value: string) => {
    setValues((v) => ({ ...v, [field]: value }));
    setClientErrors((e) => ({ ...e, [field]: undefined }));
    setLastCreated(null);
    if (createOrder.isError) createOrder.reset();
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    const errors = validateOrderForm(values);
    setClientErrors(errors);
    if (Object.keys(errors).length > 0) return;

    createOrder.mutate(
      { clienteNombre: values.clienteNombre.trim(), sku: values.sku, cantidad: Number(values.cantidad) },
      {
        onSuccess: (order) => {
          setLastCreated(order);
          setValues(EMPTY);
          onCreated?.(order);
        },
      },
    );
  };

  return (
    <form className="card form" onSubmit={handleSubmit} noValidate>
      <h2>Nuevo pedido</h2>

      {lastCreated && (
        <Alert kind="success">
          Pedido de <strong>{lastCreated.clienteNombre}</strong> registrado. Está <strong>pendiente</strong> mientras
          Inventario reserva el stock; míralo cambiar en la pestaña Pedidos.
        </Alert>
      )}
      {generalError && <Alert kind="error">{generalError}</Alert>}
      {products.isError && <Alert kind="error">No se pudo cargar el catálogo: {products.error.message}</Alert>}

      <Field label="Cliente" htmlFor="clienteNombre" error={fieldError('clienteNombre')}>
        <input
          id="clienteNombre"
          value={values.clienteNombre}
          onChange={(e) => update('clienteNombre', e.target.value)}
          placeholder="Nombre del cliente"
          maxLength={120}
          aria-invalid={!!fieldError('clienteNombre')}
        />
      </Field>

      <Field label="Producto" htmlFor="sku" error={fieldError('sku')}>
        <select
          id="sku"
          value={values.sku}
          onChange={(e) => update('sku', e.target.value)}
          disabled={products.isPending}
          aria-invalid={!!fieldError('sku')}
        >
          <option value="">{products.isPending ? 'Cargando catálogo…' : 'Selecciona un producto'}</option>
          {products.data?.map((p) => (
            <option key={p.sku} value={p.sku}>
              {p.sku} · {p.nombre}
            </option>
          ))}
        </select>
      </Field>

      <Field label={`Cantidad (${MIN_CANTIDAD}–${MAX_CANTIDAD})`} htmlFor="cantidad" error={fieldError('cantidad')}>
        <input
          id="cantidad"
          type="number"
          inputMode="numeric"
          min={MIN_CANTIDAD}
          max={MAX_CANTIDAD}
          value={values.cantidad}
          onChange={(e) => update('cantidad', e.target.value)}
          aria-invalid={!!fieldError('cantidad')}
        />
      </Field>

      <button type="submit" disabled={createOrder.isPending}>
        {createOrder.isPending ? 'Creando…' : 'Crear pedido'}
      </button>
    </form>
  );
}

function hasFieldErrors(error: ApiError | null) {
  return !!error && Object.keys(error.fieldErrors).length > 0;
}

interface FieldProps {
  label: string;
  htmlFor: string;
  error?: string;
  children: ReactNode;
}

function Field({ label, htmlFor, error, children }: FieldProps) {
  return (
    <div className={`field ${error ? 'field-invalid' : ''}`}>
      <label htmlFor={htmlFor}>{label}</label>
      {children}
      {error && (
        <p className="field-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
