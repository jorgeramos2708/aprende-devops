import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, ArrowLeft } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface TechNode {
  id: string
  title: string
  level?: string
  type?: string
}

interface TechnologyDetail {
  name: string
  description?: string
  levels?: string[]
  nodes?: TechNode[]
}

export function TechnologyView() {
  const { slug } = useParams<{ slug: string }>()
  const { data, isLoading, isError } = useQuery<TechnologyDetail>({
    queryKey: ['technology', slug],
    queryFn: async () => (await api.get(`/learning/technologies/${slug}`)).data ?? {},
    enabled: !!slug,
  })

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Cargando tecnología…
      </div>
    )
  }

  if (isError || !data) {
    return <p className="text-sm text-red-600">No se pudo cargar la tecnología.</p>
  }

  return (
    <div className="space-y-6">
      <Link to="/roadmap" className="inline-flex items-center gap-1 text-sm text-primary-600 hover:text-primary-700">
        <ArrowLeft className="w-4 h-4" /> Roadmap
      </Link>
      <div>
        <h1 className="text-2xl font-bold text-dark-900">{data.name ?? slug}</h1>
        <p className="text-dark-600">{data.description ?? 'Niveles básico, intermedio y experto.'}</p>
      </div>

      {(data.levels ?? ['Básico', 'Intermedio', 'Experto']).map((level) => (
        <Card key={level}>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>{level}</CardTitle>
              <Badge variant="primary">Nivel</Badge>
            </div>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {(data.nodes ?? [])
                .filter((n) => !n.level || n.level === level)
                .map((n) => (
                  <Link key={n.id} to={`/lesson/${n.id}`} className="block p-3 rounded-lg border border-dark-200 hover:border-primary-400 hover:bg-primary-50/50 transition-colors">
                    <span className="font-medium text-dark-900">{n.title}</span>
                    {n.type && <Badge variant="outline" size="sm">{n.type}</Badge>}
                  </Link>
                ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
