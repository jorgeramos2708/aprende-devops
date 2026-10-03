import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { BookOpen, FlaskConical, Trophy, Award } from 'lucide-react'
import { api } from '../../lib/api'
import { useAuth } from '../../lib/auth'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface RoadmapData {
  technologies?: { slug: string; name: string; level?: string }[]
}

const shortcuts = [
  { to: '/roadmap', label: 'Explorar roadmap', desc: 'Tecnologías de básico a experto', icon: BookOpen },
  { to: '/exams', label: 'Exámenes', desc: 'Valida tu nivel por módulo', icon: Trophy },
  { to: '/certifications', label: 'Certificaciones', desc: 'Simulacros de certificación', icon: Award },
  { to: '/insights', label: 'Insights', desc: 'Tu progreso y recomendaciones', icon: FlaskConical },
]

export function Home() {
  const { user } = useAuth()
  const { data } = useQuery<RoadmapData>({
    queryKey: ['roadmap'],
    queryFn: async () => (await api.get('/learning/roadmap')).data ?? {},
  })

  const techs = data?.technologies ?? []

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">
          Hola{user?.displayName ? `, ${user.displayName}` : ''} 👋
        </h1>
        <p className="text-dark-600">Sigue tu roadmap DevOps donde lo dejaste.</p>
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
