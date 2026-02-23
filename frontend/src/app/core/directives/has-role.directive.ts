import { Directive, Input, TemplateRef, ViewContainerRef, inject, effect } from '@angular/core';
import { AuthService } from '../services/auth.service';

/**
 * Structural directive that conditionally includes a template
 * only if the current user has one of the required roles.
 *
 * Usage:
 *   <button *appHasRole="['Admin']">Supprimer</button>
 *   <button *appHasRole="['Admin', 'Manager']">Valider</button>
 */
@Directive({
  selector: '[appHasRole]',
  standalone: true
})
export class HasRoleDirective {
  private readonly auth = inject(AuthService);
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private hasView = false;
  private requiredRoles: string[] = [];

  @Input() set appHasRole(roles: string[]) {
    this.requiredRoles = roles;
    this.updateView();
  }

  constructor() {
    // Re-evaluate whenever the user's role signal changes (zoneless-safe)
    effect(() => {
      this.auth.role();          // track the signal
      this.updateView();
    });
  }

  private updateView(): void {
    if (this.requiredRoles.length === 0) return;

    const allowed = this.auth.hasRole(this.requiredRoles);

    if (allowed && !this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!allowed && this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
    }
  }
}
