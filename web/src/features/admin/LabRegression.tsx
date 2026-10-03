import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, Play } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'

interface RegressionRun {
  id: string
  suiteId: string
  technologyVersion: string
  status: string
  output?: string
  completedAt: string
}

export function LabRegression() {
  const queryClient = useQueryClient()
  const [suiteId, setSuiteId] = useState('')
  const [version, setVersion] = useState('latest')

  const { data, isLoading, isError } = useQuery<RegressionRun[]>({
    queryKey: ['regression-runs'],
    queryFn: async () => (await api.get('/admin/lab-regression/runs')).data ?? [],
  })

  const run = useMutation({
    mutationFn: async () =>
      (await api.post('/admin/lab-regression/run', {
        suiteId: suiteId || null,
        technologyVersion: version,
      })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['regression-runs'] }),
  })

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-semibold text-dark-900">Regresión de laboratorios</h2>

      <Card>
        <CardHeader>
          <CardTitle>Ejecutar regresión</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <Input label="Suite ID (vacío = todas)" value={suiteId} onChange={(e) => setSuiteId(e.target.value)} placeholder="Ulid opcional" />
          <Input label="Versión de tecnología" value={version} onChange={(e) => setVersion(e.target.value)} />
          <Button onClick={() => run.mutate()} loading={run.isPending}>
            <Play className="w-4 h-4 mr-2" /> Ejecutar
          </Button>
          {run.isError && <p className="text-sm text-red-600">Falló la ejecución.</p>}
        </CardContent>
      </Card>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar las ejecuciones.</p>}

      {(data ?? []).map((r) => (
        <Card key={r.id}>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="font-mono text-sm">{r.suiteId} @ {r.technologyVersion}</CardTitle>
              <Badge variant={r.status === 'pass' ? 'success' : r.status === 'running' ? 'warning' : 'danger'}>
                {r.status}
              </Badge>
            </div>
          </CardHeader>
          {r.output && (
            <CardContent>
              <pre className="text-xs bg-dark-900 text-dark-100 p-3 rounded-lg overflow-x-auto max-h-48">{r.output}</pre>
            </CardContent>
          )}
        </Card>
      ))}
    </div>
  )
}
