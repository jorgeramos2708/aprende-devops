import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, ArrowLeft, FlaskConical } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardContent } from '../../components/Card'
import { Button } from '../../components/Button'
import { MarkdownRenderer } from '../../components/MarkdownRenderer'

interface Lesson {
  title: string
  content?: string
  labId?: string
  children?: { id: string; title: string }[]
}

export function LessonView() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError } = useQuery<Lesson>({
    queryKey: ['lesson', id],
    queryFn: async () => (await api.get(`/learning/nodes/${id}`)).data ?? {},
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Cargando lección…
      </div>
    )
  }

  if (isError || !data) {
    return <p className="text-sm text-red-600">No se pudo cargar la lección.</p>
  }

  return (
    <div className="space-y-6 max-w-4xl">
      <Link to="/roadmap" className="inline-flex items-center gap-1 text-sm text-primary-600 hover:text-primary-700">
        <ArrowLeft className="w-4 h-4" /> Roadmap
      </Link>
      <h1 className="text-2xl font-bold text-dark-900">{data.title ?? 'Lección'}</h1>

      {data.content && (
        <Card>
          <CardContent className="p-6">
            <MarkdownRenderer content={data.content} />
          </CardContent>
        </Card>
      )}

      {data.labId && (
        <Link to={`/lab/${data.labId}`}>
          <Button>
            <FlaskConical className="w-4 h-4 mr-2" /> Abrir laboratorio
          </Button>
        </Link>
      )}

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
