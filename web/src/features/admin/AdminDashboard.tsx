import { NavLink, Outlet } from 'react-router-dom'
import clsx from 'clsx'

const tabs = [
  { to: 'tech-changes', label: 'Cambios Tech' },
  { to: 'impact', label: 'Impacto' },
  { to: 'proposals', label: 'Propuestas' },
  { to: 'regression', label: 'Regresión' },
]

export function AdminDashboard() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-dark-900">Administración</h1>
      <nav className="flex flex-wrap gap-2 border-b border-dark-200 pb-3">
        {tabs.map((t) => (
          <NavLink
            key={t.to}
            to={t.to}
            className={({ isActive }) =>
              clsx(
                'px-3 py-2 rounded-lg text-sm font-medium transition-colors',
                isActive ? 'bg-primary-50 text-primary-700' : 'text-dark-600 hover:bg-dark-100',
              )
            }
          >
            {t.label}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </div>
  )
}
