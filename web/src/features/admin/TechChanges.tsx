import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, RefreshCw } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'

interface TechChange {
  id: string
  technology: string
  changeType: string
  previousVersion?: string
  newVersion?: string
  summary: string
  severity: string
  status: string
  detectedAt: string
}

export function TechChanges() {
  const queryClient = useQueryClient()
  const [filter, setFilter] = useState('')
  const { data, isLoading, isError } = useQuery<TechChange[]>({
    queryKey: ['tech-changes'],
    queryFn: async () => (await api.get('/admin/tech-changes')).data ?? [],
  })

  const check = useMutation({
    mutationFn: async () =>
      (await api.post(`/admin/tech-changes/check${filter ? `?technology=${encodeURIComponent(filter)}` : ''}`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tech-changes'] }),
  })

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h2 className="text-xl font-semibold text-dark-900">Cambios detectados</h2>
        <div className="flex flex-nowrap items-center gap-2">
          <Input placeholder="tecnología (opcional)" value={filter} onChange={(e) => setFilter(e.target.value)} />
          <Button onClick={() => check.mutate()} loading={check.isPending} className="whitespace-nowrap">
            <RefreshCw className="w-4 h-4 mr-2 shrink-0" /> Verificar ahora
          </Button>
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar los cambios.</p>}

      {(data ?? []).map((c) => (
        <Card key={c.id}>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle>{c.technology}: {c.previousVersion ?? '?'} → {c.newVersion ?? '?'}</CardTitle>
              <div className="flex gap-2">
                <Badge variant={c.severity === 'critical' ? 'danger' : c.severity === 'high' ? 'warning' : 'primary'}>
                  {c.severity}
                </Badge>
                <Badge variant="outline">{c.status}</Badge>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-dark-600">{c.summary}</p>
            <p className="text-xs text-dark-400 mt-1">{c.changeType} · {new Date(c.detectedAt).toLocaleString()}</p>
          </CardContent>
        </Card>
      ))}

      {!isLoading && (data ?? []).length === 0 && !isError && (
        <p className="text-sm text-dark-500">Sin cambios registrados. Ejecuta una verificación.</p>
      )}
    </div>
  )
}
