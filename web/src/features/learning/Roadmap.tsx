import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface Technology {
  slug: string
  name: string
  description?: string
  level?: string
}

export function Roadmap() {
  const { data, isLoading, isError } = useQuery<Technology[]>({
    queryKey: ['roadmap-technologies'],
    queryFn: async () => (await api.get('/learning/roadmap')).data?.technologies ?? [],
  })

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">Roadmap DevOps</h1>
        <p className="text-dark-600">De básico a experto, con laboratorios y exámenes por nivel.</p>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando roadmap…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudo cargar el roadmap.</p>}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {(data ?? []).map((t) => (
          <Link key={t.slug} to={`/technology/${t.slug}`}>
            <Card variant="hover" className="h-full">
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle>{t.name}</CardTitle>
                  {t.level && <Badge variant="outline">{t.level}</Badge>}
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-dark-600">{t.description ?? 'Módulo con teoría, laboratorio y examen.'}</p>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>

      {!isLoading && (data ?? []).length === 0 && !isError && (
        <p className="text-sm text-dark-500">Aún no hay tecnologías publicadas.</p>
      )}
    </div>
  )
}
