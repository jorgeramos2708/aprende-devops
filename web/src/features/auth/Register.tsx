import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../lib/auth'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'

export function Register() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await register(email, password, displayName)
      navigate('/', { replace: true })
    } catch {
      setError('No se pudo crear la cuenta. Intenta de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-dark-50 p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Crear cuenta</CardTitle>
          <p className="text-sm text-dark-500">Gratis y open source, para siempre</p>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <Input label="Nombre" required value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Tu nombre" />
            <Input label="Correo" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="tu@correo.com" />
            <Input label="Contraseña" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Mínimo 8 caracteres" minLength={8} />
            {error && <p className="text-sm text-red-600" role="alert">{error}</p>}
            <Button type="submit" loading={loading} className="w-full">Registrarse</Button>
          </form>
          <p className="mt-4 text-sm text-center text-dark-600">
            ¿Ya tienes cuenta? <Link to="/login" className="text-primary-600 hover:text-primary-700 font-medium">Entrar</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  )
}
