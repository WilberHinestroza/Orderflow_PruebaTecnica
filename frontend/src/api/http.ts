/**
 * Error tipado para todo lo que viene del backend. Si es un 400 de validación,
 * `fieldErrors` trae los mensajes por campo (formato ProblemDetails de ASP.NET).
 */
export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      ...init,
      headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...init?.headers },
    });
  } catch {
    throw new ApiError('No se pudo conectar con el servidor. Verifica que los servicios estén arriba.', 0);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(messageFor(response.status, problem), response.status, normalizeKeys(problem?.errors));
  }

  return (await response.json()) as T;
}

function messageFor(status: number, problem: ProblemDetails | null): string {
  if (status >= 500) return 'El servidor tuvo un problema. Intenta de nuevo en unos segundos.';
  if (status === 404) return 'No se encontró el recurso solicitado.';
  return problem?.title ?? problem?.detail ?? `Error inesperado (${status}).`;
}

// ASP.NET puede devolver las llaves en PascalCase ("Cantidad") o camelCase según el origen del error.
function normalizeKeys(errors?: Record<string, string[]>): Record<string, string[]> {
  if (!errors) return {};
  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => [key.charAt(0).toLowerCase() + key.slice(1), messages]),
  );
}
