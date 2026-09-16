import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  template: `
    <footer class="app-page-footer">
      <span>©2026 Perceptron Software Labs Pvt. Ltd</span>
      <span class="divider"></span>
      <span>Real-Time Ladle Monitoring System</span>
      <span class="divider"></span>
      <span>Version 1.0.0</span>
    </footer>
  `,
  styles: [`
    .app-page-footer {
      position: fixed;
      bottom: 0;
      left: 65px;
      right: 0;
      z-index: 100;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 14px;
      padding: 10px 24px;
      background: var(--color-bg-card, #ffffff);
      color: var(--color-text-muted, #64748b);
      font-size: 12px;
      border-top: 1px solid var(--color-border, #e2e8f0);
      transition: left 0.3s ease, background-color 0.3s ease, border-color 0.3s ease;
      flex-wrap: wrap;
      text-align: center;
    }

    :host-context(body.dark-theme) .app-page-footer {
      background: #0b1422;
      color: rgba(220, 230, 255, 0.6);
      border-top: 1px solid rgba(255, 255, 255, 0.08);
    }

    :host-context(.sidebar-open) .app-page-footer {
      left: 280px;
    }

    .divider {
      width: 2px;
      height: 14px;
      border-radius: 2px;
      background: var(--color-border, #e2e8f0);
    }

    :host-context(body.dark-theme) .divider {
      background: rgba(255, 255, 255, 0.12);
    }

    @media (max-width: 767px) {
      .app-page-footer,
      :host-context(.sidebar-open) .app-page-footer {
        left: 0;
      }
    }
  `]
})
export class FooterComponent {}
