import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { BookOpen, FlaskConical, Trophy, Award, PlayCircle, CheckCircle2, Timer, Compass } from 'lucide-react'
import { api } from '../../lib/api'
import { useAuth } from '../../lib/auth'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'

interface RoadmapData {
  technologies?: { slug: string; name: string; level?: string }[]
}

interface ProgressItem {
  nodeId: string
  nodeTitle: string
  status: string
  timeSpentSeconds: number
  lastAccessedAt: string
}

const shortcuts = [
  { to: '/roadmap', label: 'Explorar roadmap', desc: 'Tecnologías de básico a experto', icon: BookOpen },
  { to: '/exams', label: 'Exámenes', desc: 'Valida tu nivel por módulo', icon: Trophy },
  { to: '/certifications', label: 'Certificaciones', desc: 'Simulacros de certificación', icon: Award },
  { to: '/insights', label: 'Insights', desc: 'Tu progreso y recomendaciones', icon: FlaskConical },
]

function formatTime(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.round((totalSeconds % 3600) / 60)
  return h > 0 ? `${h}h ${m}m` : `${m}m`
}

export function Home() {
  const { user } = useAuth()
  const { data } = useQuery<RoadmapData>({
    queryKey: ['roadmap'],
    queryFn: async () => (await api.get('/learning/roadmap')).data ?? {},
  })
  const progressQuery = useQuery<ProgressItem[]>({
    queryKey: ['my-progress'],
    queryFn: async () => (await api.get('/learning/progress')).data as ProgressItem[],
  })

  const techs = data?.technologies ?? []
  const progress = progressQuery.data ?? []
  const completed = progress.filter((p) => p.status === 'completed' || p.status === 'mastered')
  const inProgress = progress.filter((p) => p.status === 'in_progress')
  const totalSeconds = progress.reduce((acc, p) => acc + (p.timeSpentSeconds ?? 0), 0)
  const lastTouched = [...progress].sort(
    (a, b) => new Date(b.lastAccessedAt).getTime() - new Date(a.lastAccessedAt).getTime()
  )[0]

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">
          Hola{user?.displayName ? `, ${user.displayName}` : ''} 👋
        </h1>
        <p className="text-dark-600">Sigue tu roadmap DevOps donde lo dejaste.</p>
      </div>

      {/* Continuar donde lo dejaste */}
      <Card className="border-primary-200 bg-primary-50/40">
        <CardContent className="flex items-center justify-between gap-4 py-5">
          <div>
            {lastTouched ? (
              <>
                <p className="text-xs font-semibold uppercase tracking-wide text-primary-700">Continúa donde lo dejaste</p>
                <p className="font-semibold text-dark-900 mt-1">{lastTouched.nodeTitle}</p>
              </>
            ) : (
              <>
                <p className="text-xs font-semibold uppercase tracking-wide text-primary-700">Empieza tu ruta</p>
                <p className="font-semibold text-dark-900 mt-1">Tu primera lección te espera en Linux</p>
              </>
            )}
          </div>
          <Link to={lastTouched ? `/lesson/${lastTouched.nodeId}` : '/technology/linux'}>
            <Button><PlayCircle className="w-4 h-4" /> {lastTouched ? 'Reanudar' : 'Empezar'}</Button>
          </Link>
        </CardContent>
      </Card>

      {/* Estadisticas rapidas */}
      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="flex items-center gap-3 py-4">
            <CheckCircle2 className="w-8 h-8 text-green-600" />
            <div>
              <p className="text-xl font-bold text-dark-900">{completed.length}</p>
              <p className="text-xs text-dark-500">Lecciones completadas</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-3 py-4">
            <Compass className="w-8 h-8 text-primary-600" />
            <div>
              <p className="text-xl font-bold text-dark-900">{inProgress.length}</p>
              <p className="text-xs text-dark-500">En progreso</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-3 py-4">
            <Timer className="w-8 h-8 text-dark-500" />
            <div>
              <p className="text-xl font-bold text-dark-900">{formatTime(totalSeconds)}</p>
              <p className="text-xs text-dark-500">Tiempo de estudio</p>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {shortcuts.map((s) => {
          const Icon = s.icon
          return (
            <Link key={s.to} to={s.to}>
              <Card variant="hover" className="h-full">
                <CardContent className="p-5">
                  <Icon className="w-8 h-8 text-primary-600 mb-3" />
                  <p className="font-semibold text-dark-900">{s.label}</p>
                  <p className="text-sm text-dark-500">{s.desc}</p>
                </CardContent>
              </Card>
            </Link>
          )
        })}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Tecnologías del roadmap</CardTitle>
        </CardHeader>
        <CardContent>
          {techs.length === 0 ? (
            <p className="text-sm text-dark-500">
              Aún no hay contenido publicado. <Link to="/roadmap" className="text-primary-600 font-medium">Ver roadmap</Link>
            </p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {techs.map((t) => (
                <Link key={t.slug} to={`/technology/${t.slug}`}>
                  <Badge variant="primary">{t.name}</Badge>
                </Link>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
