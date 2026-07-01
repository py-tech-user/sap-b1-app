import { Component, inject, signal, computed } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ROLE_NAV_ITEMS, RoleNavItem } from '../../../core/models/permissions';

@Component({
  selector:   'app-shell',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="shell-container">
      @if (sidenavOpen()) {
        <div class="sidenav-overlay" (click)="closeSidenav()"></div>
      }

      <!-- a”€a”€ Sidenav a”€a”€ -->
      @if (sidenavOpen()) {
        <aside class="sidenav">
          <div class="brand">
            <button type="button" class="sidenav-close" (click)="closeSidenav()">Fermer</button>
          </div>

          <nav class="nav-list">
            @for (item of visibleNavItems(); track item.label) {
              @if (item.children?.length) {
                <div class="nav-group">
                  <div class="nav-item nav-parent">
                    <span>{{ item.label }}</span>
                  </div>
                  <div class="nav-submenu">
                    @for (child of visibleChildren(item); track child.route) {
                      <button type="button"
                              (click)="onNavItemClick(child.route!)"
                              [class.active-link]="isRouteActive(child.route!)"
                              class="nav-item nav-btn nav-subitem">
                        <span>{{ child.label }}</span>
                      </button>
                    }
                  </div>
                </div>
              } @else {
                <button type="button"
                        (click)="onNavItemClick(item.route!)"
                        [class.active-link]="isRouteActive(item.route!)"
                        class="nav-item nav-btn">
                  <span>{{ item.label }}</span>
                </button>
              }
            }
          </nav>

          
        </aside>
      }

      <!-- a”€a”€ Main Content a”€a”€ -->
      <div class="main-content">
        <!-- Toolbar -->
        <header class="toolbar">
          <button class="menu-btn" (click)="sidenavOpen.set(!sidenavOpen())">Menu</button>

          <div class="toolbar-center">
            @if (notification.visible()) {
              <div class="notification-banner" [class.success]="notification.isSuccess()" [class.error]="notification.isError()">
                {{ notification.message() }}
              </div>
            }
          </div>

          <div class="user-section">
            <button class="user-btn" (click)="showUserMenu = !showUserMenu">
              {{ auth.currentUser()?.fullName ?? 'Utilisateur' }} v
            </button>
            @if (showUserMenu) {
              <div class="user-dropdown">
                <div class="user-menu-header">
                  <strong>{{ auth.currentUser()?.username }}</strong>
                  <small>{{ auth.currentUser()?.role }}</small>
                </div>
                <hr />
                <button class="dropdown-item" (click)="auth.logout(); showUserMenu = false">
                  Se deconnecter
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
      height: 100dvh;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    }

    /* a”€a”€ Sidenav a”€a”€ */
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
      justify-content: flex-end;
      gap: 10px;
      padding: 18px 16px;
      border-bottom: 1px solid rgba(255,255,255,0.1);
    }
    .sidenav-close {
      border: 1px solid rgba(255,255,255,0.35);
      color: #fff;
      background: transparent;
      border-radius: 6px;
      padding: 0.25rem 0.5rem;
      cursor: pointer;
      font-size: 0.78rem;
    }
    .sidenav-overlay { display: none; }
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
    .nav-group {
      display: grid;
      gap: 1px;
      flex-shrink: 0;
    }
    .nav-parent {
      color: rgba(255,255,255,0.62);
      cursor: default;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.02em;
      text-transform: uppercase;
    }
    .nav-parent:hover { background: transparent; }
    .nav-submenu {
      display: grid;
      gap: 1px;
      padding-left: 10px;
    }
    .nav-subitem {
      font-size: 13px;
      padding-left: 18px;
    }

    .sidenav-footer {
      padding: 12px 16px;
      font-size: 11px;
      color: rgba(255,255,255,0.35);
      border-top: 1px solid rgba(255,255,255,0.08);
    }

    /* a”€a”€ Main a”€a”€ */
    .main-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    /* a”€a”€ Toolbar a”€a”€ */
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

    .toolbar-center {
      flex: 1;
      display: flex;
      justify-content: center;
      align-items: center;
      min-width: 0;
      padding: 0 12px;
    }

    .notification-banner {
      max-width: 100%;
      padding: 0.45rem 0.9rem;
      border-radius: 999px;
      font-size: 13px;
      font-weight: 700;
      text-align: center;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .notification-banner.success { background: #e8f5e9; color: #1b5e20; border: 1px solid #a5d6a7; }
    .notification-banner.error { background: #ffebee; color: #b71c1c; border: 1px solid #ef9a9a; }

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

    /* a”€a”€ Page a”€a”€ */
    .page-wrapper {
      padding: 24px;
      flex: 1;
      overflow-y: auto;
      background: #f5f7fa;
    }

    @media (max-width: 900px) {
      .toolbar {
        padding: 0 10px;
        height: 52px;
      }

      .menu-btn {
        font-size: 18px;
        padding: 6px;
      }

      .user-btn {
        font-size: 12px;
        padding: 4px 6px;
      }

      .page-wrapper {
        padding: 12px;
      }

      .sidenav {
        position: fixed;
        inset: 0 auto 0 0;
        width: min(84vw, 280px);
        z-index: 1200;
        box-shadow: 10px 0 28px rgba(15, 23, 42, 0.28);
      }
      .sidenav-overlay {
        display: block;
        position: fixed;
        inset: 0;
        background: rgba(15, 23, 42, 0.3);
        z-index: 1199;
      }
    }
  `]
})
export class ShellComponent {
  auth        = inject(AuthService);
  notification = inject(NotificationService);
  private readonly router = inject(Router);
  sidenavOpen = signal(true);
  showUserMenu = false;

  /** Navigation items filtered by the current user's role */
  visibleNavItems = computed(() => {
    const currentRole = this.auth.role();
    return ROLE_NAV_ITEMS.filter(item => item.roles.includes(currentRole));
  });

  visibleChildren(item: RoleNavItem): RoleNavItem[] {
    const currentRole = this.auth.role();
    return (item.children ?? []).filter(child => child.roles.includes(currentRole));
  }

  onNavItemClick(route: string): void {
    this.showUserMenu = false;
    if (typeof window !== 'undefined' && window.innerWidth <= 900) {
      this.sidenavOpen.set(false);
    }
    this.router.navigateByUrl(route);
  }

  isRouteActive(route: string): boolean {
    return this.router.isActive(route, {
      paths: route === '/dashboard' ? 'exact' : 'subset',
      queryParams: 'ignored',
      fragment: 'ignored',
      matrixParams: 'ignored'
    });
  }

  closeSidenav(): void {
    this.sidenavOpen.set(false);
  }
}


