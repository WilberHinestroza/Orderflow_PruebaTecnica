import type { ReactNode } from 'react';

interface AlertProps {
  kind: 'error' | 'success' | 'info';
  children: ReactNode;
}

export function Alert({ kind, children }: AlertProps) {
  return (
    <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  );
}
