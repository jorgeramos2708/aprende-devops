import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface DomainReadiness {
  domainName: string
  weight: number
  readiness: number
  coveredNodes: string[]
  missingNodes: string[]
}

interface Readiness {
  certificationId: string
  certificationCode: string
  certificationName: string
  overallReadiness: number
  domains: DomainReadiness[]
  recommendedNodes: string[]
}

export function CertificationReadiness() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError } = useQuery<Readiness>({
    queryKey: ['readiness', id],
    queryFn: async () => (await api.get(`/certifications/${id}/readiness`)).data,
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Calculando preparación…
      </div>
    )
  }

  if (isError || !data) {
    return <p className="text-sm text-red-600">No se pudo calcular la preparación.</p>
  }

  return (
    <div className="space-y-6 max-w-3xl">
      <div>
        <h1 className="text-2xl font-bold text-dark-900">{data.certificationName}</h1>
        <p className="text-dark-600">
          Preparación global: <strong>{data.overallReadiness}%</strong>
        </p>
      </div>

      <div className="w-full bg-dark-100 rounded-full h-3">
        <div className="bg-primary-600 h-3 rounded-full transition-all" style={{ width: `${data.overallReadiness}%` }} />
      </div>

      {data.domains.map((d) => (
        <Card key={d.domainName}>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>{d.domainName}</CardTitle>
              <Badge variant={d.readiness >= 70 ? 'success' : d.readiness >= 40 ? 'warning' : 'danger'}>
                {d.readiness}%
              </Badge>
            </div>
          </CardHeader>
          <CardContent>
            <div className="w-full bg-dark-100 rounded-full h-2 mb-2">
              <div className="bg-primary-500 h-2 rounded-full" style={{ width: `${d.readiness}%` }} />
            </div>
            <p className="text-xs text-dark-500">
              {d.coveredNodes.length} temas cubiertos · {d.missingNodes.length} pendientes
            </p>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
