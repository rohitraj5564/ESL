import re
import io

path = r'D:\Projects\Projects\ESL\Latest\ESL_Dashboard\ESL_Dashboard\src\app\pages\dashboard\dashboard.component.css'

with io.open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Remove old host block
content = re.sub(r':host\s*\{[^\}]+\}', ':host {\n  display: block;\n  font-family: Inter, Roboto, "Segoe UI", sans-serif;\n}', content, count=1)

# Remove light mode block
content = re.sub(r'/\* --- LIGHT MODE OVERRIDES --- \*/\s*\.light-mode\s*\{[^\}]+\}', '', content)

# Variable replacements
replacements = {
    'var(--bg-root)': 'var(--color-dashboard-bg)',
    'var(--bg-header)': 'var(--color-bg-header)',
    'var(--bg-card)': 'var(--color-dashboard-card)',
    'var(--bg-chart-card)': 'var(--color-dashboard-chart-card)',
    'var(--bg-pill)': 'var(--color-dashboard-pill)',
    'var(--text-main)': 'var(--color-dashboard-text)',
    'var(--text-muted)': 'var(--color-dashboard-text-muted)',
    'var(--border-color)': 'var(--color-dashboard-border)',
    'var(--shadow-color)': 'var(--color-dashboard-shadow)',
    'var(--chip-bg)': 'var(--color-dashboard-chip-bg)',
    'var(--chip-active)': 'var(--color-dashboard-chip-active)',
    'var(--chip-text-active)': 'var(--color-dashboard-chip-text-active)',
    'var(--chart-split-line)': 'var(--color-dashboard-chart-split)',
    '#06b6d4': 'var(--color-chart-accent)',
    '#22d3ee': 'var(--color-chart-accent-hover)',
    '#22c55e': 'var(--color-accent)'
}

for old, new in replacements.items():
    content = content.replace(old, new)

responsive = '''/* =========================
   RESPONSIVE QUERIES
========================= */
@media (max-width: 1440px) {
}

@media (max-width: 1200px) {
  .chart-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  .kpi-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 1024px) {
  .chart-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .dashboard-root {
    padding: 10px;
  }
  .top-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }
  .title {
    justify-content: center;
  }
  .top-actions {
    justify-content: center;
    width: 100%;
  }
  .filter-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }
  .filter-bar .chips {
    justify-content: center;
  }
  .divider {
    display: none;
  }
  .kpi-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 10px;
  }
  .chart-grid,
  .chart-grid-two,
  .chart-grid-one {
    grid-template-columns: 1fr !important;
    gap: 12px;
  }
  .title h1 {
    font-size: 16px;
    text-align: center;
  }
  .kpi-value {
    font-size: 24px;
  }
}

@media (max-width: 480px) {
  .kpi-grid {
    grid-template-columns: 1fr;
  }
  .date-fields {
    flex-direction: column;
  }
  .date-arrow {
    transform: rotate(90deg);
    align-self: center;
  }
  .modal-footer {
    flex-direction: column;
  }
  .btn-cancel,
  .btn-download {
    width: 100%;
    justify-content: center;
  }
}'''

content = re.sub(r'/\* =========================\s*RESPONSIVE QUERIES\s*========================= \*/.*?(?=/\* Reserve space for the floating sidebar toggle button \*/)', responsive + '\n\n', content, flags=re.DOTALL)

with io.open(path, 'w', encoding='utf-8') as f:
    f.write(content)
