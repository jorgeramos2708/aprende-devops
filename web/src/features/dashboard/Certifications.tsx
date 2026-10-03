import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface Certification {
  id: string
  code: string
  name: string
  vendor: string
  version?: string
}

export function Certifications() {
  const { data, isLoading, isError } = useQuery<Certification[]>({
    queryKey: ['certifications'],
    queryFn: async () => (await api.get('/certifications')).data ?? [],
  })

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">Certificaciones</h1>
        <p className="text-dark-600">Simulacros alineados a certificaciones de la industria.</p>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar las certificaciones.</p>}

      <div className="grid gap-4 md:grid-cols-2">
        {(data ?? []).map((c) => (
          <Link key={c.id} to={`/certification/${c.id}/readiness`}>
            <Card variant="hover" className="h-full">
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <CardTitle>{c.name}</CardTitle>
                  <Badge variant="primary">{c.code}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-dark-600">{c.vendor}{c.version ? ` · v${c.version}` : ''}</p>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  )
}
