import { Component, inject, signal, computed } from '@angular/core';
import { Router, RouterOutlet, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ROLE_NAV_ITEMS } from '../../../core/models/permissions';

@Component({
  selector:   'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLinkActive],
  template: `
    <div class="shell-container">
      <!-- ── Sidenav ── -->
      @if (sidenavOpen()) {
        <aside class="sidenav">
          <div class="brand">
          </div>

          <nav class="nav-list">
            @for (item of visibleNavItems(); track item.route) {
              <button type="button"
                      (click)="onNavItemClick(item.route)"
                      routerLinkActive="active-link"
                      [routerLinkActiveOptions]="{ exact: item.route === '/dashboard' }"
                      class="nav-item nav-btn">
                <span class="nav-icon">{{ item.icon }}</span>
                <span>{{ item.label }}</span>
              </button>
            }
          </nav>

          <div class="sidenav-footer">
            <span>v1.0.0 • SAP B1</span>
          </div>
        </aside>
      }

      <!-- ── Main Content ── -->
      <div class="main-content">
        <!-- Toolbar -->
        <header class="toolbar">
          <button class="menu-btn" (click)="sidenavOpen.set(!sidenavOpen())">☰</button>
          <span class="toolbar-spacer"></span>
          <div class="user-section">
            <button class="user-btn" (click)="showUserMenu = !showUserMenu">
              👤 {{ auth.currentUser()?.fullName ?? 'Utilisateur' }} ▾
            </button>
            @if (showUserMenu) {
              <div class="user-dropdown">
                <div class="user-menu-header">
                  <strong>{{ auth.currentUser()?.username }}</strong>
                  <small>{{ auth.currentUser()?.role }}</small>
                </div>
                <hr />
                <button class="dropdown-item" (click)="auth.logout(); showUserMenu = false">
                  🚪 Se déconnecter
                </button>
              </div>
            }
          </div>
        </header>

        <!-- Page -->
        <div class="page-wrapper">
          <router-outlet />
        </div>
      </div>
    </div>
  `,
  styles: [`
    .shell-container {
      display: flex;
      height: 100vh;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    }

    /* ── Sidenav ── */
    .sidenav {
      width: 260px;
      background: #1e2a3a;
      color: white;
      display: flex;
      flex-direction: column;
      flex-shrink: 0;
      overflow: hidden;
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 18px 16px;
      border-bottom: 1px solid rgba(255,255,255,0.1);
    }
    .nav-list {
      padding: 6px 8px;
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 1px;
      overflow-y: auto;
      min-height: 0;
    }
    .nav-list::-webkit-scrollbar { width: 4px; }
    .nav-list::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.15); border-radius: 4px; }
    .nav-list::-webkit-scrollbar-track { background: transparent; }

    .nav-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 14px;
      color: rgba(255,255,255,0.8);
      border-radius: 8px;
      font-size: 13.5px;
      transition: background 0.2s;
      flex-shrink: 0;
    }
    .nav-btn {
      width: 100%;
      border: 0;
      background: transparent;
      text-align: left;
      cursor: pointer;
    }
    .nav-item:hover { background: rgba(255,255,255,0.08); }
    .nav-item.active-link {
      background: #1976d2;
      color: white;
    }
    .nav-icon { font-size: 18px; width: 24px; text-align: center; }

    .sidenav-footer {
      padding: 12px 16px;
      font-size: 11px;
      color: rgba(255,255,255,0.35);
      border-top: 1px solid rgba(255,255,255,0.08);
    }

    /* ── Main ── */
    .main-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    /* ── Toolbar ── */
    .toolbar {
      display: flex;
      align-items: center;
      padding: 0 16px;
      height: 56px;
      background: white;
      border-bottom: 1px solid #e0e0e0;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
      flex-shrink: 0;
    }

    .menu-btn {
      background: none;
      border: none;
      font-size: 20px;
      cursor: pointer;
      padding: 8px;
      border-radius: 4px;
    }
    .menu-btn:hover { background: #f0f0f0; }

    .toolbar-spacer { flex: 1; }

    .user-section { position: relative; }

    .user-btn {
      display: flex;
      align-items: center;
      gap: 4px;
      background: none;
      border: none;
      cursor: pointer;
      font-size: 14px;
      padding: 6px 10px;
      border-radius: 4px;
    }
    .user-btn:hover { background: #f0f0f0; }

    .user-dropdown {
      position: absolute;
      top: 100%;
      right: 0;
      background: white;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
      min-width: 200px;
      z-index: 200;
    }

    .user-menu-header {
      display: flex;
      flex-direction: column;
      padding: 12px 16px 8px;
    }
    .user-menu-header small { color: #888; font-size: 12px; }

    .dropdown-item {
      display: flex;
      align-items: center;
      gap: 8px;
      width: 100%;
      padding: 10px 16px;
      background: none;
      border: none;
      cursor: pointer;
      font-size: 14px;
      text-align: left;
    }
    .dropdown-item:hover { background: #f5f5f5; }

    /* ── Page ── */
    .page-wrapper {
      padding: 24px;
      flex: 1;
      overflow-y: auto;
      background: #f5f7fa;
    }
  `]
})
export class ShellComponent {
  auth        = inject(AuthService);
  private readonly router = inject(Router);
  sidenavOpen = signal(true);
  showUserMenu = false;

  /** Navigation items filtered by the current user's role */
  visibleNavItems = computed(() => {
    const currentRole = this.auth.role();
    return ROLE_NAV_ITEMS.filter(item => item.roles.includes(currentRole));
  });

  onNavItemClick(route: string): void {
    this.showUserMenu = false;
    this.router.navigateByUrl(route);
  }
}
