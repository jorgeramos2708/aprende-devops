import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Loader2, Play, Square, CheckCircle } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Button } from '../../components/Button'
import { Terminal } from '../../components/Terminal'

interface Lab {
  name: string
  description?: string
  baseImage?: string
  timeoutSeconds?: number
  metadata?: { tasks?: string[]; level?: string; estimatedTimeMinutes?: number }
}

export function LabView() {
  const { id } = useParams<{ id: string }>()
  const [attemptId, setAttemptId] = useState<string | null>(null)
  const [validation, setValidation] = useState<string | null>(null)

  const { data: lab, isLoading } = useQuery<Lab>({
    queryKey: ['lab', id],
    queryFn: async () => (await api.get(`/labs/${id}`)).data ?? {},
    enabled: !!id,
  })

  const start = useMutation({
    mutationFn: async () => (await api.post('/labs/start', { labEnvironmentId: id })).data,
    onSuccess: (data) => {
      const aid = data?.attemptId ?? data?.AttemptId ?? null
      setAttemptId(aid)
      setValidation(null)
    },
  })

  const stop = useMutation({
    mutationFn: async () => {
      if (!attemptId) return
      await api.post(`/labs/${attemptId}/stop`)
      setAttemptId(null)
    },
  })

  const validate = useMutation({
    mutationFn: async () => (await api.post(`/labs/${attemptId}/validate`)).data,
    onSuccess: (data) => setValidation(JSON.stringify(data, null, 2)),
  })

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Cargando laboratorio…
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-dark-900">{lab?.name ?? 'Laboratorio'}</h1>
          <p className="text-dark-600">{lab?.description ?? 'Practica en una terminal real.'}</p>
        </div>
        <div className="flex gap-2">
          {!attemptId ? (
            <Button onClick={() => start.mutate()} loading={start.isPending}>
              <Play className="w-4 h-4 mr-2" /> Iniciar laboratorio
            </Button>
          ) : (
            <>
              <Button variant="secondary" onClick={() => validate.mutate()} loading={validate.isPending}>
                <CheckCircle className="w-4 h-4 mr-2" /> Validar
              </Button>
              <Button variant="danger" onClick={() => stop.mutate()} loading={stop.isPending}>
                <Square className="w-4 h-4 mr-2" /> Terminar
              </Button>
            </>
          )}
        </div>
      </div>

      {start.isError && <p className="text-sm text-red-600">No se pudo iniciar el laboratorio.</p>}

      {attemptId ? (
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="lg:col-span-2">
            <Terminal attemptId={attemptId} />
          </div>

          {/* Tareas numeradas y explicitas del laboratorio */}
          <Card className="h-fit">
            <CardHeader>
              <CardTitle>Tareas</CardTitle>
            </CardHeader>
            <CardContent>
              {(lab?.metadata?.tasks?.length ?? 0) > 0 ? (
                <ol className="space-y-2 list-none">
                  {lab!.metadata!.tasks!.map((t, i) => (
                    <li key={i} className="flex gap-2 text-sm text-dark-700">
                      <span className="flex-shrink-0 w-5 h-5 rounded-full bg-primary-100 text-primary-800 text-xs font-bold flex items-center justify-center">
                        {i + 1}
                      </span>
                      <span>{t}</span>
                    </li>
                  ))}
                </ol>
              ) : (
                <p className="text-sm text-dark-500">Laboratorio libre: explora y valida cuando quieras.</p>
              )}
            </CardContent>
          </Card>
        </div>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>Sesión terminada o sin iniciar</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-dark-600">
              Las sesiones de laboratorio son efímeras: si sales a mitad, deberás empezar de nuevo.
            </p>
          </CardContent>
        </Card>
      )}

      {validation && (
        <Card>
          <CardHeader>
            <CardTitle>Resultado de validación</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="text-xs bg-dark-900 text-dark-100 p-4 rounded-lg overflow-x-auto">{validation}</pre>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
