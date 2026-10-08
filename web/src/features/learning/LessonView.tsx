import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, ArrowLeft, ArrowRight, FlaskConical, CheckCircle2 } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardContent } from '../../components/Card'
import { Button } from '../../components/Button'
import { Badge } from '../../components/Badge'
import { MarkdownRenderer } from '../../components/MarkdownRenderer'

interface Lesson {
  title: string
  content?: string
  technology?: string
  level?: string
  order?: number
  labId?: string
  prevId?: string | null
  nextId?: string | null
  children?: { id: string; title: string }[]
}

interface ProgressItem {
  nodeId: string
  status: string
}

export function LessonView() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()

  const lessonQuery = useQuery<Lesson>({
    queryKey: ['lesson', id],
    queryFn: async () => (await api.get(`/learning/nodes/${id}`)).data as Lesson,
    enabled: !!id,
  })

  const progressQuery = useQuery<ProgressItem[]>({
    queryKey: ['my-progress'],
    queryFn: async () => (await api.get('/learning/progress')).data as ProgressItem[],
  })

  const complete = useMutation({
    mutationFn: async () =>
      api.post('/learning/progress', { nodeId: id, status: 'completed', timeSpentSeconds: 0 }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['my-progress'] })
    },
  })

  const data = lessonQuery.data

  if (lessonQuery.isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Cargando lección…
      </div>
    )
  }

  if (lessonQuery.isError || !data) {
    return <p className="text-sm text-red-600">No se pudo cargar la lección.</p>
  }

  const isDone = (progressQuery.data ?? []).some(
    (p) => p.nodeId === id && (p.status === 'completed' || p.status === 'mastered')
  )
  const backTo = data.technology ? `/technology/${data.technology}` : '/roadmap'

  return (
    <div className="space-y-6 max-w-4xl">
      <Link to={backTo} className="inline-flex items-center gap-1 text-sm text-primary-600 hover:text-primary-700">
        <ArrowLeft className="w-4 h-4" /> {data.technology ? data.technology : 'Roadmap'}
      </Link>

      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          {data.level && <Badge variant="outline">Nivel {data.level}</Badge>}
          <h1 className="text-2xl font-bold text-dark-900">{data.title ?? 'Lección'}</h1>
        </div>
        {isDone ? (
          <Badge variant="success"><CheckCircle2 className="w-3 h-3 mr-1" /> Completada</Badge>
        ) : (
          <Button size="sm" onClick={() => complete.mutate()} loading={complete.isPending}>
            Marcar como completada
          </Button>
        )}
      </div>

      {data.content && (
        <Card>
          <CardContent className="p-6">
            <MarkdownRenderer content={data.content} />
          </CardContent>
        </Card>
      )}

      <div className="flex items-center justify-between gap-3">
        {data.labId && (
          <Link to={`/lab/${data.labId}`}>
            <Button variant="secondary">
              <FlaskConical className="w-4 h-4 mr-2" /> Abrir laboratorio
            </Button>
          </Link>
        )}
        <div className="flex gap-2 ml-auto">
          {data.prevId && (
            <Link to={`/lesson/${data.prevId}`}>
              <Button variant="ghost" size="sm"><ArrowLeft className="w-4 h-4" /> Anterior</Button>
            </Link>
          )}
          {data.nextId && (
            <Link to={`/lesson/${data.nextId}`}>
              <Button variant="ghost" size="sm">Siguiente <ArrowRight className="w-4 h-4" /></Button>
            </Link>
          )}
        </div>
      </div>

      {(data.children ?? []).length > 0 && (
        <div className="space-y-2">
          <h2 className="text-lg font-semibold text-dark-900">Continúa con</h2>
          {(data.children ?? []).map((c) => (
            <Link key={c.id} to={`/lesson/${c.id}`} className="block p-3 rounded-lg border border-dark-200 hover:border-primary-400 transition-colors">
              <span className="font-medium text-dark-900">{c.title}</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
