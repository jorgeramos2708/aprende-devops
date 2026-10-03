import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, Clock, Target } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface Exam {
  id: string
  title: string
  description?: string
  technology?: string
  level?: string
  passingScore: number
  timeLimitMinutes?: number
  questionCount: number
}

export function Exams() {
  const { data, isLoading, isError } = useQuery<Exam[]>({
    queryKey: ['exams'],
    queryFn: async () => (await api.get('/assessment/exams')).data ?? [],
  })

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">Exámenes</h1>
        <p className="text-dark-600">Valida tu nivel por tecnología y nivel.</p>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando exámenes…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar los exámenes.</p>}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {(data ?? []).map((e) => (
          <Link key={e.id} to={`/exam/${e.id}`}>
            <Card variant="hover" className="h-full">
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <CardTitle>{e.title}</CardTitle>
                  {e.level && <Badge variant="outline">{e.level}</Badge>}
                </div>
              </CardHeader>
              <CardContent className="space-y-2">
                <p className="text-sm text-dark-600">{e.description ?? 'Sin descripción.'}</p>
                <div className="flex flex-wrap gap-2 text-xs text-dark-500">
                  {e.technology && <Badge variant="primary" size="sm">{e.technology}</Badge>}
                  <span className="inline-flex items-center gap-1"><Target className="w-3 h-3" /> Aprueba con {e.passingScore}%</span>
                  {e.timeLimitMinutes && <span className="inline-flex items-center gap-1"><Clock className="w-3 h-3" /> {e.timeLimitMinutes} min</span>}
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>

      {!isLoading && (data ?? []).length === 0 && !isError && (
        <p className="text-sm text-dark-500">Aún no hay exámenes publicados.</p>
      )}
    </div>
  )
}
