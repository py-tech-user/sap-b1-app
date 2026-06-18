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
    this.state.set({ message: this.normalizeMessage(message), kind });

    if (this.clearTimer !== null) {
      clearTimeout(this.clearTimer);
    }

    this.clearTimer = setTimeout(() => {
      this.state.set(null);
      this.clearTimer = null;
    }, 4000);
  }

  private normalizeMessage(message: string): string {
    return String(message ?? '')
      .replace(/Ã©/g, 'é')
      .replace(/Ã¨/g, 'è')
      .replace(/Ãª/g, 'ê')
      .replace(/Ã«/g, 'ë')
      .replace(/Ã /g, 'à')
      .replace(/Ã¢/g, 'â')
      .replace(/Ã¹/g, 'ù')
      .replace(/Ã»/g, 'û')
      .replace(/Ã§/g, 'ç')
      .replace(/Ã´/g, 'ô')
      .replace(/Ã®/g, 'î')
      .replace(/Ã¯/g, 'ï')
      .replace(/Ã‰/g, 'É');
  }
}

