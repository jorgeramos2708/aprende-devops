/* eslint-disable react-refresh/only-export-components -- AuthProvider + useAuth conviven a proposito */
import { createContext, useContext, useState, useEffect, ReactNode } from 'react'
import { useLocation, Navigate } from 'react-router-dom'
import { api } from './api'

interface User {
  id: string
  email: string
  displayName: string
  roles: string[]
  avatarUrl?: string
}

interface AuthContextType {
  user: User | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  logout: () => void
  refreshToken: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const initAuth = async () => {
      const token = localStorage.getItem('access_token')
      if (token) {
        try {
          const res = await api.get('/auth/me')
          setUser(res.data)
        } catch {
          localStorage.removeItem('access_token')
          localStorage.removeItem('refresh_token')
        }
      }
      setLoading(false)
    }
    initAuth()
  }, [])

  const login = async (email: string, password: string) => {
    const res = await api.post('/auth/login', { email, password })
    const { accessToken, refreshToken, user: userData } = res.data
    localStorage.setItem('access_token', accessToken)
    localStorage.setItem('refresh_token', refreshToken)
    api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`
    setUser(userData)
  }

  const register = async (email: string, password: string, displayName: string) => {
    const res = await api.post('/auth/register', { email, password, displayName })
    const { accessToken, refreshToken, user: userData } = res.data
    localStorage.setItem('access_token', accessToken)
    localStorage.setItem('refresh_token', refreshToken)
    api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`
    setUser(userData)
  }

  const logout = () => {
    localStorage.removeItem('access_token')
    localStorage.removeItem('refresh_token')
    delete api.defaults.headers.common['Authorization']
    setUser(null)
  }

  const refreshToken = async () => {
    const refresh = localStorage.getItem('refresh_token')
    if (!refresh) return
    try {
      const res = await api.post('/auth/refresh', { refreshToken: refresh })
      const { accessToken } = res.data
      localStorage.setItem('access_token', accessToken)
      api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`
    } catch {
      logout()
    }
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout, refreshToken }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}

export function ProtectedRoute({ 
  children, 
  allowedRoles = [] 
}: { 
  children: ReactNode 
  allowedRoles?: string[] 
}) {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="w-8 h-8 border-4 border-primary-600 border-t-transparent rounded-full animate-spin" />
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (allowedRoles.length > 0 && !allowedRoles.some(role => user.roles.includes(role))) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
