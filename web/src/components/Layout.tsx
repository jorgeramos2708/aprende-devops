import { Outlet, NavLink, useLocation } from 'react-router-dom'
import { Menu, ChevronDown, Home, BookOpen, Terminal, Trophy, Award, Lightbulb, User, Settings, Shield, GitBranch, Bug, FlaskConical } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useState } from 'react'
import clsx from 'clsx'

interface ProgressItem {
  nodeId: string
  status: string
}

// Mini-resumen de progreso en la barra lateral (#6)
function SidebarProgress() {
  const { data } = useQuery<ProgressItem[]>({
    queryKey: ['my-progress'],
    queryFn: async () => (await api.get('/learning/progress')).data as ProgressItem[],
    staleTime: 60_000,
  })
  const completed = (data ?? []).filter((p) => p.status === 'completed' || p.status === 'mastered').length
  const inProgress = (data ?? []).filter((p) => p.status === 'in_progress').length

  return (
    <div className="mx-4 mb-3 rounded-lg border border-primary-100 bg-primary-50 px-3 py-2">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-primary-700">Tu progreso</p>
      <p className="text-xs text-primary-800">
        {completed} completadas{inProgress > 0 ? ` · ${inProgress} en curso` : ''}
      </p>
    </div>
  )
}

export function Layout() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)

  const navItems = [
    { path: '/', label: 'Inicio', icon: Home },
    { path: '/roadmap', label: 'Roadmap', icon: BookOpen },
    { path: '/exams', label: 'Exámenes', icon: Trophy },
    { path: '/certifications', label: 'Certificaciones', icon: Award },
    { path: '/insights', label: 'Insights', icon: Lightbulb },
  ]

  const adminItems = [
    { path: '/admin/tech-changes', label: 'Cambios Tech', icon: GitBranch },
    { path: '/admin/impact', label: 'Análisis Impacto', icon: FlaskConical },
    { path: '/admin/proposals', label: 'Propuestas', icon: Bug },
    { path: '/admin/regression', label: 'Regresión Labs', icon: Terminal },
  ]

  return (
    <div className="min-h-screen bg-dark-50">
      {/* Mobile sidebar backdrop */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 z-40 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar: SIEMPRE fixed (el error lg:static apilaba el contenido debajo) */}
      <aside className={clsx(
        'fixed inset-y-0 left-0 z-50 w-64 bg-white border-r border-dark-200 transform transition-transform duration-300',
        sidebarOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
      )}>
        <div className="flex flex-col h-full">
          {/* Logo */}
          <div className="p-4 border-b border-dark-200">
            <NavLink to="/" className="flex items-center gap-2" onClick={() => setSidebarOpen(false)}>
              <div className="w-8 h-8 bg-primary-600 rounded-lg flex items-center justify-center">
                <span className="text-white font-mono font-bold text-lg">$</span>
              </div>
              <span className="font-bold text-xl text-dark-900">DevOps Learn</span>
            </NavLink>
          </div>

          {/* Navigation */}
          <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
            {navItems.map(item => {
              const Icon = item.icon
              const isActive = location.pathname === item.path ||
                (item.path !== '/' && location.pathname.startsWith(item.path))
              return (
                <NavLink
                  key={item.path}
                  to={item.path}
                  onClick={() => setSidebarOpen(false)}
                  className={clsx(
                    'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-primary-50 text-primary-700'
                      : 'text-dark-600 hover:bg-dark-100 hover:text-dark-900'
                  )}
                >
                  <Icon className="w-5 h-5 flex-shrink-0" />
                  {item.label}
                </NavLink>
              )
            })}

            {user?.roles?.includes('admin') && (
              <>
                <div className="pt-4 pb-2 px-3">
                  <span className="text-xs font-semibold text-dark-400 uppercase tracking-wider">Administración</span>
                </div>
                {adminItems.map(item => {
                  const Icon = item.icon
                  const isActive = location.pathname.startsWith(item.path)
                  return (
                    <NavLink
                      key={item.path}
                      to={item.path}
                      onClick={() => setSidebarOpen(false)}
                      className={clsx(
                        'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                        isActive
                          ? 'bg-primary-50 text-primary-700'
                          : 'text-dark-600 hover:bg-dark-100 hover:text-dark-900'
                      )}
                    >
                      <Icon className="w-5 h-5 flex-shrink-0" />
                      {item.label}
                    </NavLink>
                  )
                })}
              </>
            )}
          </nav>

          <SidebarProgress />

          {/* User section (menu se abre hacia arriba, pegado al usuario) */}
          <div className="p-4 border-t border-dark-200 relative">
            {userMenuOpen && (
              <div className="absolute bottom-full left-4 right-4 mb-2 w-auto bg-white rounded-lg shadow-lg border border-dark-200 py-1 z-50">
                <NavLink
                  to="/profile"
                  className="flex items-center gap-2 px-3 py-2 text-sm text-dark-700 hover:bg-dark-50"
                  onClick={() => setUserMenuOpen(false)}
                >
                  <User className="w-4 h-4" />
                  Perfil
                </NavLink>
                <NavLink
                  to="/settings"
                  className="flex items-center gap-2 px-3 py-2 text-sm text-dark-700 hover:bg-dark-50"
                  onClick={() => setUserMenuOpen(false)}
                >
                  <Settings className="w-4 h-4" />
                  Configuración
                </NavLink>
                <hr className="my-1 border-dark-200" />
                <button
                  onClick={logout}
                  className="flex items-center gap-2 w-full px-3 py-2 text-sm text-red-600 hover:bg-red-50"
                >
                  <Shield className="w-4 h-4" />
                  Cerrar sesión
                </button>
              </div>
            )}
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-primary-100 flex items-center justify-center">
                <span className="text-primary-700 font-medium text-sm">
                  {user?.displayName?.[0]?.toUpperCase() || user?.email?.[0]?.toUpperCase() || 'U'}
                </span>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-dark-900 truncate">{user?.displayName || 'Usuario'}</p>
                <p className="text-xs text-dark-500 truncate">{user?.email}</p>
              </div>
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="p-1.5 rounded-lg text-dark-500 hover:bg-dark-100 hover:text-dark-700 transition-colors"
              >
                <ChevronDown className={clsx('w-5 h-5 transition-transform', userMenuOpen && 'rotate-180')} />
              </button>
            </div>
          </div>
        </div>
      </aside>

      {/* Main content */}
      <div className="lg:pl-64">
        {/* Top bar (solo movil) */}
        <header className="sticky top-0 z-30 bg-white border-b border-dark-200 lg:hidden">
          <div className="flex items-center justify-between h-16 px-4">
            <button
              onClick={() => setSidebarOpen(true)}
              className="p-2 rounded-lg text-dark-600 hover:bg-dark-100"
            >
              <Menu className="w-6 h-6" />
            </button>
            <span className="font-bold text-dark-900">DevOps Learn</span>
            <div className="w-8" />
          </div>
        </header>

        <main className="p-4 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
