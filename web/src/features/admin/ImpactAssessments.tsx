import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface Impact {
  id: string
  changeId: string
  overallImpact: string
  recommendation?: string
  assessedAt: string
}

export function ImpactAssessments() {
  const { data, isLoading, isError } = useQuery<Impact[]>({
    queryKey: ['impact'],
    queryFn: async () => (await api.get('/admin/impact-assessments')).data ?? [],
  })

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-semibold text-dark-900">Análisis de impacto</h2>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar los análisis.</p>}

      {(data ?? []).map((a) => (
        <Card key={a.id}>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="font-mono text-sm">{a.changeId}</CardTitle>
              <Badge variant={a.overallImpact === 'high' ? 'danger' : a.overallImpact === 'medium' ? 'warning' : 'success'}>
                {a.overallImpact}
              </Badge>
            </div>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-dark-600">{a.recommendation ?? 'Sin recomendación.'}</p>
            <p className="text-xs text-dark-400 mt-1">{new Date(a.assessedAt).toLocaleString()}</p>
          </CardContent>
        </Card>
      ))}

      {!isLoading && (data ?? []).length === 0 && !isError && (
        <p className="text-sm text-dark-500">Sin análisis. Se generan al detectar cambios.</p>
      )}
    </div>
  )
}
