import { Link, useParams } from 'react-router-dom'
import { useQueries } from '@tanstack/react-query'
import { Loader2, ArrowLeft, CheckCircle2, Circle, Clock, Lock, PlayCircle } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'

interface TechNode {
  id: string
  title: string
  level: string
  type: string
  module?: string
  order: number
  minutes: number
}

interface TechnologyDetail {
  name: string
  description?: string
  levels?: string[]
  nodes: TechNode[]
}

interface ProgressItem {
  nodeId: string
  status: string
}

interface ExamItem {
  id: string
  title: string
  technology?: string
  level?: string
  passingScore: number
  timeLimitMinutes?: number
  questionCount: number
}

const LEVEL_LABELS: Record<string, string> = {
  basic: 'Básico',
  intermediate: 'Intermedio',
  advanced: 'Experto',
  expert: 'Experto',
}

export function TechnologyView() {
  const { slug } = useParams<{ slug: string }>()

  const [techQuery, progressQuery, examsQuery] = useQueries({
    queries: [
      {
        queryKey: ['technology', slug],
        queryFn: async () => (await api.get(`/learning/technologies/${slug}`)).data as TechnologyDetail,
        enabled: !!slug,
      },
      {
        queryKey: ['my-progress'],
        queryFn: async () => (await api.get('/learning/progress')).data as ProgressItem[],
      },
      {
        queryKey: ['exams'],
        queryFn: async () => (await api.get('/assessment/exams')).data as ExamItem[],
      },
    ],
  })

  const data = techQuery.data
  const isLoading = techQuery.isLoading

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Cargando tecnología…
      </div>
    )
  }

  if (techQuery.isError || !data) {
    return <p className="text-sm text-red-600">No se pudo cargar la tecnología.</p>
  }

  const done = new Set((progressQuery.data ?? []).filter((p) => p.status === 'completed' || p.status === 'mastered').map((p) => p.nodeId))
  const lessons = (data.nodes ?? []).filter((n) => n.type === 'Lesson')
  const completedCount = lessons.filter((n) => done.has(n.id)).length
  const pct = lessons.length > 0 ? Math.round((completedCount / lessons.length) * 100) : 0

  const levelIds = (data.levels ?? ['basic', 'intermediate', 'advanced']).filter((l) =>
    lessons.some((n) => n.level === l) || (examsQuery.data ?? []).some((e) => e.technology === slug && e.level === l)
  )

  return (
    <div className="space-y-8">
      <div className="space-y-3">
        <Link to="/roadmap" className="inline-flex items-center gap-1 text-sm text-primary-600 hover:text-primary-700">
          <ArrowLeft className="w-4 h-4" /> Roadmap
        </Link>
        <h1 className="text-2xl font-bold text-dark-900">{data.name}</h1>
        {data.description && <p className="text-dark-600 max-w-3xl">{data.description}</p>}

        <div className="flex items-center gap-3">
          <div className="flex-1 h-2 bg-dark-100 rounded-full overflow-hidden max-w-md">
            <div className="h-full bg-primary-600 rounded-full transition-all" style={{ width: `${pct}%` }} />
          </div>
          <span className="text-sm text-dark-600">
            {completedCount} de {lessons.length} lecciones ({pct}%)
          </span>
        </div>
      </div>

      {levelIds.map((level) => {
        const levelLessons = lessons.filter((n) => n.level === level)
        const levelDone = levelLessons.filter((n) => done.has(n.id)).length
        const allDone = levelLessons.length > 0 && levelDone === levelLessons.length
        const exam = (examsQuery.data ?? []).find((e) => e.technology === slug && e.level === level)

        return (
          <section key={level} className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-dark-900">
                Nivel {LEVEL_LABELS[level] ?? level}
              </h2>
              <Badge variant={allDone ? 'success' : 'outline'}>
                {levelDone}/{levelLessons.length} completadas
              </Badge>
            </div>

            <div className="space-y-2" />
            <Card>
              <CardContent className="p-0 divide-y divide-dark-100">
                {levelLessons.map((n) => {
                  const isDone = done.has(n.id)
                  return (
                    <Link
                      key={n.id}
                      to={`/lesson/${n.id}`}
                      className="flex items-center gap-3 px-4 py-3 hover:bg-primary-50/50 transition-colors"
                    >
                      {isDone ? (
                        <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0" />
                      ) : (
                        <Circle className="w-5 h-5 text-dark-300 shrink-0" />
                      )}
                      <span className="flex-1 font-medium text-dark-900">
                        {n.order > 0 ? `${n.order}. ` : ''}{n.title}
                      </span>
                      {n.minutes > 0 && (
                        <span className="inline-flex items-center gap-1 text-xs text-dark-500">
                          <Clock className="w-3 h-3" /> {n.minutes} min
                        </span>
                      )}
                      {n.module && <Badge variant="outline" size="sm">{n.module}</Badge>}
                    </Link>
                  )
                })}
                {levelLessons.length === 0 && (
                  <p className="px-4 py-3 text-sm text-dark-500">Contenido en preparación.</p>
                )}
              </CardContent>
            </Card>

            {exam && (
              <Card className="border-primary-200 bg-primary-50/40">
                <CardContent className="flex items-center justify-between gap-4 py-4">
                  <div>
                    <p className="font-medium text-dark-900">{exam.title}</p>
                    <p className="text-xs text-dark-500">
                      {exam.questionCount} preguntas · aprueba con {exam.passingScore}%
                      {exam.timeLimitMinutes ? ` · ${exam.timeLimitMinutes} min` : ''}
                    </p>
                  </div>
                  {allDone ? (
                    <Link to={`/exam/${exam.id}`}>
                      <Button size="sm"><PlayCircle className="w-4 h-4" /> Iniciar examen</Button>
                    </Link>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-xs text-dark-500">
                      <Lock className="w-3 h-3" /> Completa las {levelLessons.length} lecciones para habilitar
                    </span>
                  )}
                </CardContent>
              </Card>
            )}
          </section>
        )
      })}
    </div>
  )
}
