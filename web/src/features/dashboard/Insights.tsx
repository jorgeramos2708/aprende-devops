import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Check, X } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'

interface Insight {
  id: string
  type: string
  title: string
  description?: string
  priority: number
  isRead: boolean
  createdAt: string
}

export function Insights() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useQuery<Insight[]>({
    queryKey: ['insights'],
    queryFn: async () => (await api.get('/insights')).data ?? [],
  })

  const act = async (kind: 'read' | 'dismiss', id: string) => {
    await api.post(`/insights/${id}/${kind}`)
    queryClient.invalidateQueries({ queryKey: ['insights'] })
  }

  return (
    <div className="space-y-6 max-w-3xl">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">Insights</h1>
        <p className="text-dark-600">Recomendaciones generadas a partir de tu progreso.</p>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar los insights.</p>}

      {(data ?? []).map((i) => (
        <Card key={i.id} className={i.isRead ? 'opacity-70' : ''}>
          <CardContent className="p-5">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <Badge variant={i.priority >= 7 ? 'danger' : i.priority >= 4 ? 'warning' : 'primary'}>
                    P{i.priority}
                  </Badge>
                  <span className="text-xs text-dark-400">{i.type}</span>
                </div>
                <p className="font-semibold text-dark-900">{i.title}</p>
                {i.description && <p className="text-sm text-dark-600 mt-1">{i.description}</p>}
              </div>
              <div className="flex gap-1 flex-shrink-0">
                {!i.isRead && (
                  <Button size="sm" variant="ghost" onClick={() => void act('read', i.id)} title="Marcar leído">
                    <Check className="w-4 h-4" />
                  </Button>
                )}
                <Button size="sm" variant="ghost" onClick={() => void act('dismiss', i.id)} title="Descartar">
                  <X className="w-4 h-4" />
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ))}

      {!isLoading && (data ?? []).length === 0 && !isError && (
        <p className="text-sm text-dark-500">Sin insights por ahora. Completa laboratorios y exámenes.</p>
      )}
    </div>
  )
}
