import { Injectable, signal } from '@angular/core';

type NotificationKind = 'success' | 'error';

interface NotificationState {
  message: string;
  kind: NotificationKind;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly state = signal<NotificationState | null>(null);
  private clearTimer: ReturnType<typeof setTimeout> | null = null;

  visible(): boolean {
    return this.state() !== null;
  }

  message(): string {
    return this.state()?.message ?? '';
  }

  isSuccess(): boolean {
    return this.state()?.kind === 'success';
  }

  isError(): boolean {
    return this.state()?.kind === 'error';
  }

  showSuccess(message: string): void {
    this.show(message, 'success');
  }

  showError(message: string): void {
    this.show(message, 'error');
  }

  clear(): void {
    if (this.clearTimer !== null) {
      clearTimeout(this.clearTimer);
      this.clearTimer = null;
    }

    this.state.set(null);
  }

  private show(message: string, kind: NotificationKind): void {
    this.state.set({ message, kind });

    if (this.clearTimer !== null) {
      clearTimeout(this.clearTimer);
    }

    this.clearTimer = setTimeout(() => {
      this.state.set(null);
      this.clearTimer = null;
    }, 4000);
  }
}

