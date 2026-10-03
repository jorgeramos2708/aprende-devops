import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, Check } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'

interface Proposal {
  id: string
  changeId: string
  title: string
  description?: string
  status: string
  createdAt: string
}

export function UpdateProposals() {
  const queryClient = useQueryClient()
  const [changeId, setChangeId] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')

  const { data, isLoading, isError } = useQuery<Proposal[]>({
    queryKey: ['proposals'],
    queryFn: async () => (await api.get('/admin/update-proposals')).data ?? [],
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['proposals'] })

  const create = useMutation({
    mutationFn: async () => (await api.post('/admin/update-proposals', { changeId, title, description })).data,
    onSuccess: () => { setChangeId(''); setTitle(''); setDescription(''); refresh() },
  })

  const approve = useMutation({
    mutationFn: async (id: string) => (await api.post(`/admin/update-proposals/${id}/approve`)).data,
    onSuccess: refresh,
  })

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-semibold text-dark-900">Propuestas de actualización</h2>

      <Card>
        <CardHeader>
          <CardTitle>Nueva propuesta</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <Input label="ID del cambio" value={changeId} onChange={(e) => setChangeId(e.target.value)} placeholder="Ulid del cambio" />
          <Input label="Título" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Actualizar labs de Docker a v27" />
          <Input label="Descripción" value={description} onChange={(e) => setDescription(e.target.value)} />
          <Button onClick={() => create.mutate()} loading={create.isPending} disabled={!changeId || !title}>
            Crear propuesta
          </Button>
          {create.isError && <p className="text-sm text-red-600">No se pudo crear.</p>}
        </CardContent>
      </Card>

      {isLoading && (
        <div className="flex items-center gap-2 text-dark-500">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando…
        </div>
      )}
      {isError && <p className="text-sm text-red-600">No se pudieron cargar las propuestas.</p>}

      {(data ?? []).map((p) => (
        <Card key={p.id}>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle>{p.title}</CardTitle>
              <Badge variant={p.status === 'approved' ? 'success' : 'outline'}>{p.status}</Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-2">
            {p.description && <p className="text-sm text-dark-600">{p.description}</p>}
            {p.status !== 'approved' && (
              <Button size="sm" variant="secondary" onClick={() => approve.mutate(p.id)} loading={approve.isPending}>
                <Check className="w-4 h-4 mr-1" /> Aprobar
              </Button>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
