import { useAuth } from '../../lib/auth'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Button } from '../../components/Button'
import { Badge } from '../../components/Badge'

export function Profile() {
  const { user, logout } = useAuth()

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-2xl font-bold text-dark-900">Perfil</h1>
      <Card>
        <CardHeader>
          <CardTitle>{user?.displayName ?? 'Usuario'}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="text-sm text-dark-500">Correo</p>
            <p className="font-medium text-dark-900">{user?.email ?? '—'}</p>
          </div>
          <div>
            <p className="text-sm text-dark-500 mb-1">Roles</p>
            <div className="flex flex-wrap gap-2">
              {(user?.roles ?? []).length === 0 ? (
                <Badge variant="outline">estudiante</Badge>
              ) : (
                (user?.roles ?? []).map((r) => <Badge key={r} variant="primary">{r}</Badge>)
              )}
            </div>
          </div>
          <Button variant="secondary" onClick={logout}>Cerrar sesión</Button>
        </CardContent>
      </Card>
    </div>
  )
}
